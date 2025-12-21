/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - RETRAIN AGENT RUNNER
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 * Async runner koji implementira agent ciklus: Sense → Think → Act → Learn
 *
 *   SENSE:  Provjeri NewGoldSinceLastTrain counter
 *   THINK:  Odluči da li treba retrenirati (counter >= threshold)
 *   ACT:    Pokreni trening novog modela i aktiviraj ga
 *   LEARN:  Reset counter, logiraj rezultat
 *
 * Ovaj runner se koristi iz BackgroundService-a u Web sloju.
 * Agent klase (RetrainAgent) ostaju kao edukativni primjer generičkog agenta.
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiAgents.SpamAgent.Domain;
using AiAgents.SpamAgent.Infrastructure;
using AiAgents.SpamAgent.Application.Services;

namespace AiAgents.SpamAgent.Application.Runners;

/// <summary>
/// Async runner za retrain agent - provjerava i eventualno retrenira model.
/// </summary>
public class RetrainAgentRunner
{
    // Bitno: BackgroundService tipično živi dugo → nemoj držati DbContext kao field.
    private readonly IDbContextFactory<SpamAgentDbContext> _dbFactory;
    private readonly TrainingService _trainingService;
    private readonly TrainTemplate _defaultTemplate;

    public RetrainAgentRunner(
        IDbContextFactory<SpamAgentDbContext> dbFactory,
        TrainingService trainingService,
        TrainTemplate defaultTemplate = TrainTemplate.Medium)
    {
        _dbFactory = dbFactory;
        _trainingService = trainingService;
        _defaultTemplate = defaultTemplate;
    }

    /// <summary>
    /// Izvršava jedan tick agent ciklusa: Sense → Think → Act → Learn.
    /// Vraća rezultat ako je retrain izvršen, null ako nije potreban.
    /// </summary>
    public async Task<RetrainTickResult?> TickAsync(CancellationToken ct = default)
    {
        // ═══════════════════════════════════════════════════════════════════
        // SENSE: Pročitaj stanje (gold counter, threshold, enabled)
        // ═══════════════════════════════════════════════════════════════════

        await using var context = await _dbFactory.CreateDbContextAsync(ct);

        var settings = await context.SystemSettings
            .AsNoTracking()
            .FirstAsync(ct);

        var state = new RetrainState
        {
            NewGoldCount = settings.NewGoldSinceLastTrain,
            Threshold = settings.RetrainGoldThreshold,
            AutoRetrainEnabled = settings.AutoRetrainEnabled,
            // Ovdje je ID aktivnog modela (ne "Version" broj).
            CurrentModelVersion = settings.ActiveModelVersionId
        };

        // ═══════════════════════════════════════════════════════════════════
        // THINK: Odluči da li retrenirati
        // ═══════════════════════════════════════════════════════════════════
        if (!state.AutoRetrainEnabled)
            return null; // Auto-retrain isključen

        if (state.Threshold <= 0)
            return null; // Prag nije validan (sigurnosna zaštita)

        if (state.NewGoldCount < state.Threshold)
            return null; // Još nedovoljno gold labela

        // ═══════════════════════════════════════════════════════════════════
        // ACT: Treniraj novi model i aktiviraj ga
        // ═══════════════════════════════════════════════════════════════════
        try
        {
            // TrainingService je odgovoran za:
            // - trening
            // - upis ModelVersion
            // - (po potrebi) aktivaciju
            // - reset counter-a NewGoldSinceLastTrain (prema tvojoj specifikaciji)
            var model = await _trainingService.TrainModelAsync(_defaultTemplate, activate: true);

            // ═══════════════════════════════════════════════════════════════════
            // LEARN: Rezultat (counter je već resetovan u TrainingService)
            // ═══════════════════════════════════════════════════════════════════
            return new RetrainTickResult
            {
                Success = true,
                NewModelVersion = model.Version,
                Template = model.TrainTemplate,
                Metrics = new RetrainMetrics
                {
                    Accuracy = model.Accuracy,
                    Precision = model.Precision,
                    Recall = model.Recall,
                    F1 = model.F1
                },
                TrainSetSize = model.TrainSetSize,
                GoldIncludedCount = model.GoldIncludedCount,
                Activated = true,
                Reason = $"Auto-retrain: {state.NewGoldCount} gold labela (threshold: {state.Threshold})",
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new RetrainTickResult
            {
                Success = false,
                Reason = $"Greška pri retrain-u: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Forsira retrain bez obzira na counter.
    /// </summary>
    public async Task<RetrainTickResult> ForceRetrainAsync(
        TrainTemplate? template = null,
        bool activate = true,
        CancellationToken ct = default)
    {
        var tmpl = template ?? _defaultTemplate;

        try
        {
            var model = await _trainingService.TrainModelAsync(tmpl, activate);

            return new RetrainTickResult
            {
                Success = true,
                NewModelVersion = model.Version,
                Template = model.TrainTemplate,
                Metrics = new RetrainMetrics
                {
                    Accuracy = model.Accuracy,
                    Precision = model.Precision,
                    Recall = model.Recall,
                    F1 = model.F1
                },
                TrainSetSize = model.TrainSetSize,
                GoldIncludedCount = model.GoldIncludedCount,
                Activated = activate,
                Reason = "Forsirani retrain",
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new RetrainTickResult
            {
                Success = false,
                Reason = $"Greška pri retrain-u: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Dohvata trenutno stanje za monitoring.
    /// </summary>
    public async Task<RetrainState> GetStateAsync(CancellationToken ct = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(ct);

        var settings = await context.SystemSettings
            .AsNoTracking()
            .FirstAsync(ct);

        return new RetrainState
        {
            NewGoldCount = settings.NewGoldSinceLastTrain,
            Threshold = settings.RetrainGoldThreshold,
            AutoRetrainEnabled = settings.AutoRetrainEnabled,
            CurrentModelVersion = settings.ActiveModelVersionId
        };
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     RESULT DTOs
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Stanje koje retrain agent opaža.
/// </summary>
public class RetrainState
{
    public int NewGoldCount { get; set; }
    public int Threshold { get; set; }
    public bool AutoRetrainEnabled { get; set; }
    public int? CurrentModelVersion { get; set; }

    public bool ShouldRetrain => AutoRetrainEnabled && Threshold > 0 && NewGoldCount >= Threshold;
    public double ProgressPercentage => Threshold > 0 ? (double)NewGoldCount / Threshold * 100 : 0;
}

/// <summary>
/// Rezultat jednog tick-a retrain agenta.
/// Web sloj koristi ovo za SignalR evente bez dodatne logike.
/// </summary>
public class RetrainTickResult
{
    public bool Success { get; set; }
    public int? NewModelVersion { get; set; }
    public TrainTemplate? Template { get; set; }
    public RetrainMetrics? Metrics { get; set; }
    public int TrainSetSize { get; set; }
    public int GoldIncludedCount { get; set; }
    public bool Activated { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Metrike novog modela.
/// </summary>
public class RetrainMetrics
{
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1 { get; set; }
}
