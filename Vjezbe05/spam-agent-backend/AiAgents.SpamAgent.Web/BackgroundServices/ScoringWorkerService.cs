/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT WEB - SCORING WORKER SERVICE
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 * Background servis koji kontinuirano procesira poruke iz queue-a.
 *
 * Koristi ScoringAgent iz Application/Agents/.
 * Nasleđuje TickWorkerServiceBase za zajedničku loop logiku.
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using AiAgents.SpamAgent.Application.Agents;
using AiAgents.SpamAgent.Web.Hubs;
using AiAgents.SpamAgent.Web.Models;

namespace AiAgents.SpamAgent.Web.BackgroundServices;

public class ScoringWorkerService : TickWorkerServiceBase<ScoringAgent, ScoringTickResult>
{
    private readonly IHubContext<SpamAgentHub> _hubContext;

    // Konfiguracija delays
    protected override int IdleDelayMs => 500;
    protected override int BusyDelayMs => 100;
    protected override int NotReadyDelayMs => 2000;
    protected override int ErrorDelayMs => 1000;

    public ScoringWorkerService(
        IServiceScopeFactory scopeFactory,
        IHubContext<SpamAgentHub> hubContext,
        ILogger<ScoringWorkerService> logger)
        : base(scopeFactory, logger)
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Obrađuje rezultat tick-a: logira i emituje SignalR event.
    /// </summary>
    protected override async Task OnTickResultAsync(
        ScoringTickResult result,
        IServiceProvider scopedServices,
        CancellationToken ct)
    {
        Logger.LogInformation(
            "Poruka {Id} procesirana: pSpam={PSpam:F3}, Decision={Decision}",
            result.MessageId, result.PSpam, result.Decision);

        // Emituj SignalR event
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
