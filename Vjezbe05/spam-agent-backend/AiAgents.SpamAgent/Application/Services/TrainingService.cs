/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - TRAINING SERVICE
 * ═══════════════════════════════════════════════════════════════════════════════
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiAgents.SpamAgent.Domain;
using AiAgents.SpamAgent.Infrastructure;
using AiAgents.SpamAgent.ML;

namespace AiAgents.SpamAgent.Application.Services;

/// <summary>
/// Servis za treniranje ML modela.
/// </summary>
public class TrainingService
{
    private readonly SpamAgentDbContext _context;
    private readonly ISpamClassifier _classifier;
    private readonly string _modelsDirectory;

    // Veličine template-a
    private static readonly Dictionary<TrainTemplate, int> TemplateSizes = new()
    {
        { TrainTemplate.Light, 500 },
        { TrainTemplate.Medium, 2000 },
        { TrainTemplate.Full, int.MaxValue }
    };

    public TrainingService(
        SpamAgentDbContext context, 
        ISpamClassifier classifier,
        string modelsDirectory)
    {
        _context = context;
        _classifier = classifier;
        _modelsDirectory = modelsDirectory;

        if (!Directory.Exists(_modelsDirectory))
        {
            Directory.CreateDirectory(_modelsDirectory);
        }
    }

    /// <summary>
    /// Trenira novi model sa zadanim template-om.
    /// </summary>
    /// <param name="template">Light/Medium/Full</param>
    /// <param name="activate">Ako true, aktivira model nakon treninga</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Nova verzija modela sa metrikama</returns>
    public async Task<ModelVersion> TrainModelAsync(
        TrainTemplate template, 
        bool activate = false,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var settings = await _context.SystemSettings.FirstAsync(ct);

        // 1. Pripremi training set
        var maxSamples = TemplateSizes[template];
        
        // UCI TrainPool poruke
        var uciTrainData = await _context.Messages
            .Where(m => m.Source == MessageSource.Uci && 
                       m.Split == DataSplit.TrainPool &&
                       m.TrueLabel != null)
            .OrderBy(m => m.Id)
            .Take(maxSamples)
            .Select(m => new { m.Text, m.TrueLabel })
            .ToListAsync(ct);

        ct.ThrowIfCancellationRequested();

        // Gold labels (sve review-ovane poruke)
        var goldData = await _context.Reviews
            .Include(r => r.Message)
            .Select(r => new { r.Message.Text, TrueLabel = (Label?)r.Label })
            .ToListAsync(ct);

        // Kombiniraj
        var trainingSamples = uciTrainData
            .Concat(goldData)
            .Where(x => x.TrueLabel != null)
            .Select(x => new TrainingSample(x.Text, x.TrueLabel == Label.Spam))
            .ToList();

        ct.ThrowIfCancellationRequested();

        // 2. Pripremi validation set (fiksno)
        var validationData = await _context.Messages
            .Where(m => m.Source == MessageSource.Uci && 
                       m.Split == DataSplit.ValidationHoldout &&
                       m.TrueLabel != null)
            .Select(m => new { m.Text, m.TrueLabel })
            .ToListAsync(ct);

        var validationSamples = validationData
            .Where(x => x.TrueLabel != null)
            .Select(x => new TrainingSample(x.Text, x.TrueLabel == Label.Spam))
            .ToList();

        ct.ThrowIfCancellationRequested();

        // 3. Odredi novu verziju
        var maxVersion = await _context.ModelVersions.MaxAsync(mv => (int?)mv.Version, ct) ?? 0;
        var newVersion = maxVersion + 1;
        var modelFileName = $"model_v{newVersion:D3}.zip";
        var modelPath = Path.Combine(_modelsDirectory, modelFileName);

        // 4. Treniraj
        await _classifier.TrainAsync(trainingSamples, modelPath);

        ct.ThrowIfCancellationRequested();

        // 5. Evaluiraj
        var metrics = await _classifier.EvaluateAsync(validationSamples);

        ct.ThrowIfCancellationRequested();

        // 6. Kreiraj ModelVersion zapis
        var modelVersion = new ModelVersion
        {
            Version = newVersion,
            TrainerType = "SDCA Logistic Regression",
            Featurizer = "FeaturizeText TF-IDF",
            TrainTemplate = template,
            TrainSetSize = trainingSamples.Count,
            GoldIncludedCount = goldData.Count,
            ValidationSetSize = validationSamples.Count,
            Accuracy = metrics.Accuracy,
            Precision = metrics.Precision,
            Recall = metrics.Recall,
            F1 = metrics.F1Score,
            ThresholdAllow = settings.ThresholdAllow,
            ThresholdBlock = settings.ThresholdBlock,
            ModelFilePath = modelPath,
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = false
        };

        _context.ModelVersions.Add(modelVersion);
        await _context.SaveChangesAsync(ct);

        // 7. Aktiviraj ako je zatraženo
        if (activate)
        {
            await ActivateModelAsync(modelVersion.Id, ct);
        }

        // 8. Resetuj gold counter
        settings.NewGoldSinceLastTrain = 0;
        settings.LastRetrainAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return modelVersion;
    }

    /// <summary>
    /// Aktivira postojeći model.
    /// </summary>
    public async Task<ModelVersion?> ActivateModelAsync(int modelVersionId, CancellationToken ct = default)
    {
        var model = await _context.ModelVersions.FindAsync(new object[] { modelVersionId }, ct);
        if (model == null) return null;

        // Deaktiviraj sve ostale
        await _context.ModelVersions
            .Where(mv => mv.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(mv => mv.IsActive, false), ct);

        // Aktiviraj traženi
        model.IsActive = true;

        // Ažuriraj SystemSettings
        var settings = await _context.SystemSettings.FirstAsync(ct);
        settings.ActiveModelVersionId = modelVersionId;

        await _context.SaveChangesAsync(ct);

        // Učitaj model u classifier
        await _classifier.LoadModelAsync(model.ModelFilePath);

        return model;
    }

    /// <summary>
    /// Vraća aktivni model.
    /// </summary>
    public async Task<ModelVersion?> GetActiveModelAsync(CancellationToken ct = default)
    {
        return await _context.ModelVersions
            .Where(mv => mv.IsActive)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Vraća sve verzije modela.
    /// </summary>
    public async Task<List<ModelVersion>> GetAllModelsAsync(CancellationToken ct = default)
    {
        return await _context.ModelVersions
            .OrderByDescending(mv => mv.Version)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Učitava aktivni model u classifier (za startup).
    /// </summary>
    public async Task<bool> LoadActiveModelAsync(CancellationToken ct = default)
    {
        var active = await GetActiveModelAsync(ct);
        if (active == null) return false;

        if (!File.Exists(active.ModelFilePath)) return false;

        await _classifier.LoadModelAsync(active.ModelFilePath);
        return true;
    }
}
