/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT WEB - SIMULATOR SERVICE
 * ═══════════════════════════════════════════════════════════════════════════════
 * 
 * Simulira dolazak novih poruka u queue.
 * Korisno za demo bez frontend-a - samo gledaš kako agent procesira.
 * 
 * Pattern: Scope per iteration (bitno za EF Core!)
 */

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using AiAgents.SpamAgent.Domain;
using AiAgents.SpamAgent.Infrastructure;
using AiAgents.SpamAgent.Application.Services;
using AiAgents.SpamAgent.Web.Hubs;
using AiAgents.SpamAgent.Web.Models;

namespace AiAgents.SpamAgent.Web.BackgroundServices;

public class SimulatorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<SpamAgentHub> _hubContext;
    private readonly ILogger<SimulatorService> _logger;
    
    private bool _enabled;
    private int _intervalMs;
    private int _batchSize;

    public SimulatorService(
        IServiceScopeFactory scopeFactory,
        IHubContext<SpamAgentHub> hubContext,
        ILogger<SimulatorService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;

        // Učitaj postavke iz konfiguracije
        _enabled = configuration.GetValue("Simulator:Enabled", false);
        _intervalMs = configuration.GetValue("Simulator:IntervalMs", 3000);
        _batchSize = configuration.GetValue("Simulator:BatchSize", 1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("Simulator je isključen.");
        }
        else
        {
            _logger.LogInformation(
                "Simulator pokrenut: interval={Interval}ms, batch={Batch}",
                _intervalMs, _batchSize);
        }

        // Čekaj malo da se sistem inicijalizira
        await Task.Delay(5000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Provjeri da li je uključen
                if (!_enabled)
                {
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                // Scope per iteration
                using var scope = _scopeFactory.CreateScope();
                await EnqueueSimulatedMessagesAsync(scope.ServiceProvider, _batchSize, stoppingToken);

                await Task.Delay(_intervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Greška u Simulator loop-u");
                await Task.Delay(5000, stoppingToken);
            }
        }

        _logger.LogInformation("Simulator zaustavljen.");
    }

    private async Task EnqueueSimulatedMessagesAsync(
        IServiceProvider serviceProvider, 
        int count, 
        CancellationToken ct)
    {
        var queueService = serviceProvider.GetRequiredService<QueueService>();
        var context = serviceProvider.GetRequiredService<SpamAgentDbContext>();

        var enqueued = await queueService.EnqueueFromValidationAsync(count, copyAsTrueLabel: true);

        if (enqueued > 0)
        {
            _logger.LogDebug("Simulator: dodano {Count} poruka u queue", enqueued);

            // Dohvati upravo dodane poruke za SignalR event
            var recentMessages = await context.Messages
                .Where(m => m.Status == MessageStatus.Queued)
                .OrderByDescending(m => m.CreatedAtUtc)
                .Take(enqueued)
                .ToListAsync(ct);

            foreach (var msg in recentMessages)
            {
                var evt = new MessageQueuedEvent
                {
                    MessageId = msg.Id,
                    Text = msg.Text.Length > 50 
                        ? msg.Text.Substring(0, 50) + "..." 
                        : msg.Text,
                    Timestamp = DateTime.UtcNow
                };

                await _hubContext.SendMessageQueued(evt);
            }
        }
    }

    /// <summary>
    /// Omogućava runtime uključivanje/isključivanje simulatora.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        _logger.LogInformation("Simulator {Status}", enabled ? "uključen" : "isključen");
    }

    /// <summary>
    /// Postavlja interval između simulacija.
    /// </summary>
    public void SetInterval(int intervalMs)
    {
        _intervalMs = Math.Max(500, intervalMs);
        _logger.LogInformation("Simulator interval postavljen na {Interval}ms", _intervalMs);
    }

    /// <summary>
    /// Postavlja veličinu batch-a.
    /// </summary>
    public void SetBatchSize(int batchSize)
    {
        _batchSize = Math.Clamp(batchSize, 1, 10);
        _logger.LogInformation("Simulator batch size postavljen na {Size}", _batchSize);
    }

    public bool IsEnabled => _enabled;
    public int IntervalMs => _intervalMs;
    public int BatchSize => _batchSize;
}
