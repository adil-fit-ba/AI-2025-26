/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT WEB - SCORING WORKER SERVICE
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 * Background servis koji kontinuirano procesira poruke iz queue-a.
 *
 * Koristi ScoringAgentRunner iz shared library-ja.
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
using AiAgents.SpamAgent.Application.Runners;
using AiAgents.SpamAgent.Web.Hubs;
using AiAgents.SpamAgent.Web.Models;

namespace AiAgents.SpamAgent.Web.BackgroundServices;

public class ScoringWorkerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<SpamAgentHub> _hubContext;
    private readonly ILogger<ScoringWorkerService> _logger;

    // Adaptive delays
    private const int IdleDelayMs = 500;
    private const int BusyDelayMs = 100;
    private const int WaitForModelDelayMs = 2000;
    private const int ErrorDelayMs = 1000;

    public ScoringWorkerService(
        IServiceScopeFactory scopeFactory,
        IHubContext<SpamAgentHub> hubContext,
        ILogger<ScoringWorkerService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScoringWorker pokrenut.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Scope per iteration - bitno za EF Core!
                using var scope = _scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<ScoringAgentRunner>();

                // Ako nema aktivnog modela, samo čekaj (ne pokušavaj score-ati)
                if (!await runner.IsReadyAsync(stoppingToken))
                {
                    _logger.LogDebug("Nema aktivnog modela. Čekam...");
                    await Task.Delay(WaitForModelDelayMs, stoppingToken);
                    continue;
                }

                // Jedan tick agenta: Sense → Think → Act
                var result = await runner.TickAsync(stoppingToken);

                if (result != null)
                {
                    _logger.LogInformation(
                        "Poruka {Id} procesirana: pSpam={PSpam:F3}, Decision={Decision}",
                        result.MessageId, result.PSpam, result.Decision);

                    // Emituj SignalR event
                    await EmitMessageScoredEventAsync(result);

                    // Kratka pauza kad ima posla
                    await Task.Delay(BusyDelayMs, stoppingToken);
                }
                else
                {
                    // Duža pauza kad je queue prazan
                    await Task.Delay(IdleDelayMs, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška u ScoringWorker loop-u");

                // Delay poslije greške, ali uredno prekini ako je cancellation
                try
                {
                    await Task.Delay(ErrorDelayMs, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("ScoringWorker zaustavljen.");
    }

    private async Task EmitMessageScoredEventAsync(ScoringTickResult result)
    {
        var evt = new MessageScoredEvent
        {
            MessageId = result.MessageId,
            TextPreview = result.TextPreview,
            PSpam = result.PSpam,
            Decision = result.Decision.ToString(),
            NewStatus = result.NewStatus.ToString(),
            TrueLabel = result.TrueLabel?.ToString(),
            IsCorrect = result.IsCorrect,
            Timestamp = result.Timestamp
        };

        await _hubContext.SendMessageScored(evt);
    }
}
