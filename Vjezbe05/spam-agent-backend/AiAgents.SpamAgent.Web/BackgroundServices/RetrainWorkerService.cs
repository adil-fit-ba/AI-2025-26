/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT WEB - RETRAIN WORKER SERVICE
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 * Background servis koji prati gold labele i automatski retrenira model.
 *
 * Koristi RetrainAgentRunner iz shared library-ja.
 * Servis je samo "host" - sva logika je u Runner-u.
 *
 * Pattern: Scope per iteration (bitno za EF Core!)
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using AiAgents.SpamAgent.Application.Queries;
using AiAgents.SpamAgent.Application.Runners;
using AiAgents.SpamAgent.Web.Hubs;
using AiAgents.SpamAgent.Web.Models;

namespace AiAgents.SpamAgent.Web.BackgroundServices;

public class RetrainWorkerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<SpamAgentHub> _hubContext;
    private readonly ILogger<RetrainWorkerService> _logger;

    // Provjera svakih 10 sekundi
    private const int CheckIntervalMs = 10000;

    // Kratki backoff ako se desi neočekivana greška
    private const int ErrorBackoffMs = 5000;

    public RetrainWorkerService(
        IServiceScopeFactory scopeFactory,
        IHubContext<SpamAgentHub> hubContext,
        ILogger<RetrainWorkerService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RetrainWorker pokrenut.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delayMs = CheckIntervalMs;

            try
            {
                // Scope per iteration - bitno za EF Core!
                using var scope = _scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<RetrainAgentRunner>();

                // Jedan tick agenta: Sense → Think → Act → Learn
                var result = await runner.TickAsync(stoppingToken);

                // Ako je retrain izvršen i uspješan
                if (result != null && result.Success)
                {
                    _logger.LogInformation(
                        "Model v{Version} automatski treniran. Accuracy: {Accuracy:P2}",
                        result.NewModelVersion,
                        result.Metrics?.Accuracy ?? 0);

                    // Emituj SignalR evente
                    await EmitModelRetrainedEventAsync(result);
                    await EmitStatsUpdateAsync(scope.ServiceProvider);
                }
                // Ako je retrain pokušao, ali nije uspio (da se ne “proguta” greška)
                else if (result != null && !result.Success)
                {
                    _logger.LogWarning("Auto-retrain nije uspio. Razlog: {Reason}", result.Reason);
                }
            }
            catch (OperationCanceledException)
            {
                // Normalan shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška u RetrainWorker loop-u");
                delayMs = ErrorBackoffMs;
            }

            // Regularni interval provjere (ili kraći backoff ako je bila greška)
            try
            {
                await Task.Delay(delayMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("RetrainWorker zaustavljen.");
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

    private async Task EmitStatsUpdateAsync(IServiceProvider serviceProvider)
    {
        var messageQuery = serviceProvider.GetRequiredService<MessageQueryService>();
        var adminQuery = serviceProvider.GetRequiredService<AdminQueryService>();

        var counts = await messageQuery.GetCountsAsync();
        var goldStats = await adminQuery.GetGoldStatsAsync();

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
