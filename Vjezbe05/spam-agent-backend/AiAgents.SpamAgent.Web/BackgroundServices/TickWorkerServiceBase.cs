/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT WEB - TICK WORKER SERVICE BASE
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 * Bazna klasa za background servise koji pokreću tick-based agente.
 * 
 * Izvlači zajedničku logiku iz ScoringWorkerService i RetrainWorkerService:
 *   - Scope per iteration (bitno za EF Core!)
 *   - Ready check prije tick-a
 *   - Adaptive delays (idle/busy/error)
 *   - Graceful shutdown sa CancellationToken
 *   - Error handling i logging
 *
 * PATTERN:
 *   1. Kreiraj scope
 *   2. Resolve agent
 *   3. Provjeri IsReady (ako agent implementira IReadyCheckAgent)
 *   4. Pozovi TickAsync
 *   5. Obradi rezultat (SignalR, logging...) kroz OnTickResultAsync hook
 *   6. Čekaj (adaptive delay)
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AiAgents.SpamAgent.Abstractions;

namespace AiAgents.SpamAgent.Web.BackgroundServices;

/// <summary>
/// Bazna klasa za background servise koji pokreću tick-based agente.
/// </summary>
/// <typeparam name="TAgent">Tip agenta (mora implementirati ITickAgent&lt;TResult&gt;)</typeparam>
/// <typeparam name="TResult">Tip rezultata tick-a</typeparam>
public abstract class TickWorkerServiceBase<TAgent, TResult> : BackgroundService
    where TAgent : class, ITickAgent<TResult>
    where TResult : class
{
    protected readonly IServiceScopeFactory ScopeFactory;
    protected readonly ILogger Logger;

    // ═══════════════════════════════════════════════════════════════════════════
    //                     KONFIGURACIJA (override u podklasi)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Delay kad je queue prazan / nema posla (default: 500ms)
    /// </summary>
    protected virtual int IdleDelayMs => 500;

    /// <summary>
    /// Delay kad ima posla (default: 100ms)
    /// </summary>
    protected virtual int BusyDelayMs => 100;

    /// <summary>
    /// Delay nakon greške (default: 1000ms)
    /// </summary>
    protected virtual int ErrorDelayMs => 1000;

    /// <summary>
    /// Delay kad agent nije spreman, npr. nema aktivnog modela (default: 2000ms)
    /// </summary>
    protected virtual int NotReadyDelayMs => 2000;

    /// <summary>
    /// Ime workera za logging (default: ime klase)
    /// </summary>
    protected virtual string WorkerName => GetType().Name;

    // ═══════════════════════════════════════════════════════════════════════════
    //                     KONSTRUKTOR
    // ═══════════════════════════════════════════════════════════════════════════

    protected TickWorkerServiceBase(
        IServiceScopeFactory scopeFactory,
        ILogger logger)
    {
        ScopeFactory = scopeFactory;
        Logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //                     MAIN LOOP
    // ═══════════════════════════════════════════════════════════════════════════

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("{Worker} pokrenut.", WorkerName);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delayMs = IdleDelayMs;

            try
            {
                // Scope per iteration - bitno za EF Core!
                using var scope = ScopeFactory.CreateScope();
                var agent = scope.ServiceProvider.GetRequiredService<TAgent>();

                // Ready check (ako agent implementira IReadyCheckAgent)
                if (!await IsAgentReadyAsync(agent, stoppingToken))
                {
                    Logger.LogDebug("{Worker}: Agent nije spreman. Čekam...", WorkerName);
                    await SafeDelayAsync(NotReadyDelayMs, stoppingToken);
                    continue;
                }

                // Tick!
                var result = await agent.TickAsync(stoppingToken);

                if (result != null)
                {
                    // Hook za obradu rezultata (SignalR, logging...)
                    await OnTickResultAsync(result, scope.ServiceProvider, stoppingToken);
                    delayMs = BusyDelayMs;
                }
                // else: nema posla, idle delay
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Greška u {Worker} loop-u", WorkerName);
                delayMs = ErrorDelayMs;
            }

            await SafeDelayAsync(delayMs, stoppingToken);
        }

        Logger.LogInformation("{Worker} zaustavljen.", WorkerName);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //                     HOOKS (override u podklasi)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Provjerava da li je agent spreman za rad.
    /// Default implementacija: ako agent implementira IReadyCheckAgent, poziva IsReadyAsync.
    /// </summary>
    protected virtual async Task<bool> IsAgentReadyAsync(TAgent agent, CancellationToken ct)
    {
        if (agent is IReadyCheckAgent readyCheck)
        {
            return await readyCheck.IsReadyAsync(ct);
        }
        return true; // Ako nema ready check, pretpostavi da je spreman
    }

    /// <summary>
    /// Hook za obradu rezultata tick-a.
    /// Override u podklasi za SignalR evente, logging, itd.
    /// </summary>
    /// <param name="result">Rezultat tick-a</param>
    /// <param name="scopedServices">Scoped service provider za resolving dodatnih servisa</param>
    /// <param name="ct">Cancellation token</param>
    protected abstract Task OnTickResultAsync(
        TResult result,
        IServiceProvider scopedServices,
        CancellationToken ct);

    // ═══════════════════════════════════════════════════════════════════════════
    //                     HELPER METODE
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Safe delay koji ne baca exception na cancellation.
    /// </summary>
    private async Task SafeDelayAsync(int milliseconds, CancellationToken ct)
    {
        try
        {
            await Task.Delay(milliseconds, ct);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown - ne propagiraj exception
        }
    }
}
