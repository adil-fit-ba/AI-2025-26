/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - ADMIN QUERY SERVICE
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 * Read-only query servis za admin operacije.
 * Kontroleri koriste ovo umjesto direktnog LINQ-a.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiAgents.SpamAgent.Domain;
using AiAgents.SpamAgent.Infrastructure;

namespace AiAgents.SpamAgent.Application.Queries;

/// <summary>
/// Query servis za admin - read-only operacije.
/// </summary>
public class AdminQueryService
{
    private readonly SpamAgentDbContext _context;

    public AdminQueryService(SpamAgentDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Kompletni status sistema.
    /// </summary>
    public async Task<SystemStatus> GetSystemStatusAsync(CancellationToken ct = default)
    {
        // Read-only: bez tracking-a
        var settings = await _context.SystemSettings
            .AsNoTracking()
            .Include(s => s.ActiveModelVersion)
            .FirstAsync(ct);

        // Dataset statistika se računa direktno iz baze (ne koristimo DatabaseSeeder u query layer-u).
        var datasetStats = await GetDatasetStatsInternalAsync(ct);

        var totalGold = await _context.Reviews
            .AsNoTracking()
            .CountAsync(ct);

        var queueCounts = await GetQueueCountsInternalAsync(ct);

        return new SystemStatus
        {
            ActiveModel = settings.ActiveModelVersion != null
                ? MapModelVersion(settings.ActiveModelVersion)
                : null,

            Settings = new SettingsInfo
            {
                ThresholdAllow = settings.ThresholdAllow,
                ThresholdBlock = settings.ThresholdBlock,
                RetrainGoldThreshold = settings.RetrainGoldThreshold,
                NewGoldSinceLastTrain = settings.NewGoldSinceLastTrain,
                AutoRetrainEnabled = settings.AutoRetrainEnabled,
                LastRetrainAtUtc = settings.LastRetrainAtUtc
            },

            QueueCounts = queueCounts,

            DatasetStats = new DatasetInfo
            {
                TotalMessages = datasetStats.TotalMessages,
                UciMessages = datasetStats.UciMessages,
                RuntimeMessages = datasetStats.RuntimeMessages,
                TrainPoolCount = datasetStats.TrainPoolCount,
                ValidationCount = datasetStats.ValidationCount,
                HamCount = datasetStats.HamCount,
                SpamCount = datasetStats.SpamCount,
                TotalGoldLabels = totalGold
            }
        };
    }

    /// <summary>
    /// Aktivni model.
    /// </summary>
    public async Task<ModelVersionInfo?> GetActiveModelAsync(CancellationToken ct = default)
    {
        // Ako se desi greška i postoji više "active", uzmi najnoviji po verziji.
        var model = await _context.ModelVersions
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderByDescending(m => m.Version)
            .FirstOrDefaultAsync(ct);

        return model != null ? MapModelVersion(model) : null;
    }

    /// <summary>
    /// Sve verzije modela.
    /// </summary>
    public async Task<List<ModelVersionInfo>> GetAllModelsAsync(CancellationToken ct = default)
    {
        var models = await _context.ModelVersions
            .AsNoTracking()
            .OrderByDescending(m => m.Version)
            .ToListAsync(ct);

        return models.Select(MapModelVersion).ToList();
    }

    /// <summary>
    /// Model po verziji.
    /// </summary>
    public async Task<ModelVersionInfo?> GetModelByVersionAsync(int version, CancellationToken ct = default)
    {
        var model = await _context.ModelVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Version == version, ct);

        return model != null ? MapModelVersion(model) : null;
    }

    /// <summary>
    /// Trenutne postavke.
    /// </summary>
    public async Task<SettingsInfo> GetSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _context.SystemSettings
            .AsNoTracking()
            .FirstAsync(ct);

        return new SettingsInfo
        {
            ThresholdAllow = settings.ThresholdAllow,
            ThresholdBlock = settings.ThresholdBlock,
            RetrainGoldThreshold = settings.RetrainGoldThreshold,
            NewGoldSinceLastTrain = settings.NewGoldSinceLastTrain,
            AutoRetrainEnabled = settings.AutoRetrainEnabled,
            LastRetrainAtUtc = settings.LastRetrainAtUtc
        };
    }

    /// <summary>
    /// Statistika gold labela.
    /// </summary>
    public async Task<GoldStats> GetGoldStatsAsync(CancellationToken ct = default)
    {
        var settings = await _context.SystemSettings
            .AsNoTracking()
            .FirstAsync(ct);

        var totalGold = await _context.Reviews
            .AsNoTracking()
            .CountAsync(ct);

        // Demo statistika treba pratiti runtime poruke (UCI import ne smije kvariti brojeve).
        var pendingCount = await _context.Messages
            .AsNoTracking()
            .Where(m => m.Source == MessageSource.Runtime && m.Status == MessageStatus.PendingReview)
            .CountAsync(ct);

        return new GoldStats
        {
            TotalGoldLabels = totalGold,
            PendingReviewCount = pendingCount,
            NewGoldSinceLastTrain = settings.NewGoldSinceLastTrain,
            RetrainGoldThreshold = settings.RetrainGoldThreshold,
            WillTriggerRetrain = settings.AutoRetrainEnabled &&
                                settings.NewGoldSinceLastTrain >= settings.RetrainGoldThreshold
        };
    }

    private async Task<QueueCounts> GetQueueCountsInternalAsync(CancellationToken ct)
    {
        var counts = await _context.Messages
            .AsNoTracking()
            .Where(m => m.Source == MessageSource.Runtime)
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

        return new QueueCounts
        {
            Queued = counts.GetValueOrDefault(MessageStatus.Queued),
            InInbox = counts.GetValueOrDefault(MessageStatus.InInbox),
            InSpam = counts.GetValueOrDefault(MessageStatus.InSpam),
            PendingReview = counts.GetValueOrDefault(MessageStatus.PendingReview)
        };
    }

    private async Task<DatasetStatsInternal> GetDatasetStatsInternalAsync(CancellationToken ct)
    {
        // Sve poruke (UCI + runtime)
        var totalMessages = await _context.Messages
            .AsNoTracking()
            .CountAsync(ct);

        // Po izvoru
        var uciMessages = await _context.Messages
            .AsNoTracking()
            .Where(m => m.Source == MessageSource.Uci)
            .CountAsync(ct);

        var runtimeMessages = await _context.Messages
            .AsNoTracking()
            .Where(m => m.Source == MessageSource.Runtime)
            .CountAsync(ct);

        // Split (tipično relevantno samo za UCI import)
        var trainPoolCount = await _context.Messages
            .AsNoTracking()
            .Where(m => m.Split == DataSplit.TrainPool)
            .CountAsync(ct);

        var validationCount = await _context.Messages
            .AsNoTracking()
            .Where(m => m.Split == DataSplit.ValidationHoldout)
            .CountAsync(ct);

        // Label statistika (gdje god je TrueLabel popunjen)
        var hamCount = await _context.Messages
            .AsNoTracking()
            .Where(m => m.TrueLabel == Label.Ham)
            .CountAsync(ct);

        var spamCount = await _context.Messages
            .AsNoTracking()
            .Where(m => m.TrueLabel == Label.Spam)
            .CountAsync(ct);

        return new DatasetStatsInternal
        {
            TotalMessages = totalMessages,
            UciMessages = uciMessages,
            RuntimeMessages = runtimeMessages,
            TrainPoolCount = trainPoolCount,
            ValidationCount = validationCount,
            HamCount = hamCount,
            SpamCount = spamCount
        };
    }

    private static ModelVersionInfo MapModelVersion(ModelVersion m)
    {
        return new ModelVersionInfo
        {
            Id = m.Id,
            Version = m.Version,
            TrainTemplate = m.TrainTemplate,
            TrainSetSize = m.TrainSetSize,
            GoldIncludedCount = m.GoldIncludedCount,
            ValidationSetSize = m.ValidationSetSize,
            Metrics = new ModelMetrics
            {
                Accuracy = m.Accuracy,
                Precision = m.Precision,
                Recall = m.Recall,
                F1 = m.F1
            },
            ThresholdAllow = m.ThresholdAllow,
            ThresholdBlock = m.ThresholdBlock,
            IsActive = m.IsActive,
            CreatedAtUtc = m.CreatedAtUtc
        };
    }

    private sealed class DatasetStatsInternal
    {
        public int TotalMessages { get; set; }
        public int UciMessages { get; set; }
        public int RuntimeMessages { get; set; }
        public int TrainPoolCount { get; set; }
        public int ValidationCount { get; set; }
        public int HamCount { get; set; }
        public int SpamCount { get; set; }
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     RESULT DTOs
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Kompletni status sistema.
/// </summary>
public class SystemStatus
{
    public ModelVersionInfo? ActiveModel { get; set; }
    public SettingsInfo Settings { get; set; } = new();
    public QueueCounts QueueCounts { get; set; } = new();
    public DatasetInfo DatasetStats { get; set; } = new();
}

/// <summary>
/// Informacije o verziji modela.
/// </summary>
public class ModelVersionInfo
{
    public int Id { get; set; }
    public int Version { get; set; }
    public TrainTemplate TrainTemplate { get; set; }
    public int TrainSetSize { get; set; }
    public int GoldIncludedCount { get; set; }
    public int ValidationSetSize { get; set; }
    public ModelMetrics Metrics { get; set; } = new();
    public double ThresholdAllow { get; set; }
    public double ThresholdBlock { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Metrike modela.
/// </summary>
public class ModelMetrics
{
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1 { get; set; }
}

/// <summary>
/// Postavke sistema.
/// </summary>
public class SettingsInfo
{
    public double ThresholdAllow { get; set; }
    public double ThresholdBlock { get; set; }
    public int RetrainGoldThreshold { get; set; }
    public int NewGoldSinceLastTrain { get; set; }
    public bool AutoRetrainEnabled { get; set; }
    public DateTime? LastRetrainAtUtc { get; set; }
}

/// <summary>
/// Statistika dataseta.
/// </summary>
public class DatasetInfo
{
    public int TotalMessages { get; set; }
    public int UciMessages { get; set; }
    public int RuntimeMessages { get; set; }
    public int TrainPoolCount { get; set; }
    public int ValidationCount { get; set; }
    public int HamCount { get; set; }
    public int SpamCount { get; set; }
    public int TotalGoldLabels { get; set; }
}

/// <summary>
/// Statistika gold labela.
/// </summary>
public class GoldStats
{
    public int TotalGoldLabels { get; set; }
    public int PendingReviewCount { get; set; }
    public int NewGoldSinceLastTrain { get; set; }
    public int RetrainGoldThreshold { get; set; }
    public bool WillTriggerRetrain { get; set; }

    public double ProgressPercentage => RetrainGoldThreshold > 0
        ? (double)NewGoldSinceLastTrain / RetrainGoldThreshold * 100
        : 0;
}
