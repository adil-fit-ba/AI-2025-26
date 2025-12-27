/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT WEB - RETRAIN WORKER SERVICE
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 * Background servis koji prati gold labele i automatski retrenira model.
 *
 * Koristi RetrainAgent iz Application/Agents/.
 * Nasleđuje TickWorkerServiceBase za zajedničku loop logiku.
 *
 * NAPOMENA: RetrainAgent ne implementira IReadyCheckAgent jer uvijek može
 * provjeriti stanje (za razliku od ScoringAgent koji treba model).
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using AiAgents.SpamAgent.Application.Agents;
using AiAgents.SpamAgent.Application.Queries;
using AiAgents.SpamAgent.Web.Hubs;
using AiAgents.SpamAgent.Web.Models;

namespace AiAgents.SpamAgent.Web.BackgroundServices;

public class RetrainWorkerService : TickWorkerServiceBase<RetrainAgent, RetrainTickResult>
{
    private readonly IHubContext<SpamAgentHub> _hubContext;

    // Provjera svakih 10 sekundi (idle = check interval)
    protected override int IdleDelayMs => 10000;
    protected override int BusyDelayMs => 1000; // Nakon retraina, kratka pauza
    protected override int ErrorDelayMs => 5000;

    public RetrainWorkerService(
        IServiceScopeFactory scopeFactory,
        IHubContext<SpamAgentHub> hubContext,
        ILogger<RetrainWorkerService> logger)
        : base(scopeFactory, logger)
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// RetrainAgent uvijek može raditi (samo provjerava stanje).
    /// </summary>
    protected override Task<bool> IsAgentReadyAsync(RetrainAgent agent, CancellationToken ct)
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// Obrađuje rezultat tick-a: logira i emituje SignalR evente.
    /// </summary>
    protected override async Task OnTickResultAsync(
        RetrainTickResult result,
        IServiceProvider scopedServices,
        CancellationToken ct)
    {
        if (result.Success)
        {
            Logger.LogInformation(
                "Model v{Version} automatski treniran. Accuracy: {Accuracy:P2}",
                result.NewModelVersion,
                result.Metrics?.Accuracy ?? 0);

            // Emituj SignalR evente
            await EmitModelRetrainedEventAsync(result);
            await EmitStatsUpdateAsync(scopedServices, ct);
        }
        else
        {
            Logger.LogWarning("Auto-retrain nije uspio. Razlog: {Reason}", result.Reason);
        }
    }

    private async Task EmitModelRetrainedEventAsync(RetrainTickResult result)
    {
        var evt = new ModelRetrainedEvent
        {
            NewVersion = result.NewModelVersion ?? 0,
            Template = result.Template?.ToString() ?? "Medium",
            Metrics = result.Metrics != null ? new MetricsDto
            {
                Accuracy = result.Metrics.Accuracy,
                Precision = result.Metrics.Precision,
                Recall = result.Metrics.Recall,
                F1 = result.Metrics.F1
            } : new MetricsDto(),
            IsActivated = result.Activated,
            Timestamp = result.Timestamp
        };

        await _hubContext.SendModelRetrained(evt);
    }

    private async Task EmitStatsUpdateAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        var messageQuery = serviceProvider.GetRequiredService<MessageQueryService>();
        var adminQuery = serviceProvider.GetRequiredService<AdminQueryService>();

        var counts = await messageQuery.GetCountsAsync(ct);
        var goldStats = await adminQuery.GetGoldStatsAsync(ct);

        var evt = new StatsUpdatedEvent
        {
            QueueStats = new QueueStatsDto
            {
                Queued = counts.Queued,
                InInbox = counts.InInbox,
                InSpam = counts.InSpam,
                PendingReview = counts.PendingReview,
                TotalProcessed = counts.TotalProcessed
            },
            NewGoldSinceLastTrain = goldStats.NewGoldSinceLastTrain,
            RetrainGoldThreshold = goldStats.RetrainGoldThreshold,
            Timestamp = DateTime.UtcNow
        };

        await _hubContext.SendStatsUpdated(evt);
    }
}
