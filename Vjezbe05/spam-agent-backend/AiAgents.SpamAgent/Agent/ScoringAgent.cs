/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - SCORING AGENT
 * ═══════════════════════════════════════════════════════════════════════════════
 * 
 * Ovaj agent implementira Sense → Think → Act ciklus za procesiranje SMS poruka:
 * 
 *   Sense:  Uzmi sljedeću poruku iz queue-a (Status=Queued)
 *   Think:  ML model izračuna pSpam vjerovatnoću
 *   Act:    Odluka (Allow/Pending/Block) + update statusa poruke
 * 
 * NAPOMENA: Learn se ne dešava u ovom agentu - to radi RetrainAgent!
 */

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiAgents.Core;
using AiAgents.SpamAgent.Domain;
using AiAgents.SpamAgent.Infrastructure;
using AiAgents.SpamAgent.Application.Services;
using System.Linq;

namespace AiAgents.SpamAgent.Agent;

// ════════════════════════════════════════════════════════════════════════════════
//                     TIPOVI ZA SCORING AGENT
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Akcija koju agent proizvodi nakon razmišljanja.
/// </summary>
public record SpamScoringAction(
    long MessageId,
    string Text,
    double PSpam,
    SpamDecision Decision
);

/// <summary>
/// Rezultat izvršenja akcije.
/// </summary>
public record SpamScoringResult(
    long MessageId,
    MessageStatus NewStatus,
    bool Success
);

// ════════════════════════════════════════════════════════════════════════════════
//                     KOMPONENTE AGENTA
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Percepcija: dohvata sljedeću poruku iz queue-a.
/// </summary>
public class MessageQueuePerception : IPerceptionSource<Message>
{
    private readonly SpamAgentDbContext _context;

    public MessageQueuePerception(SpamAgentDbContext context)
    {
        _context = context;
    }

    public Message? Observe()
    {
        // Sinhrono za sada - u produkciji bi bilo async
        return _context.Messages
            .FirstOrDefault(m => m.Status == MessageStatus.Queued);
    }

    public bool HasNext => _context.Messages.Any(m => m.Status == MessageStatus.Queued);
}

/// <summary>
/// Politika: ML model koji odlučuje na osnovu teksta.
/// </summary>
public class SpamScoringPolicy : IPolicy<Message, SpamScoringAction>
{
    private readonly ScoringService _scoringService;
    private readonly SpamAgentDbContext _context;

    public SpamScoringPolicy(ScoringService scoringService, SpamAgentDbContext context)
    {
        _scoringService = scoringService;
        _context = context;
    }

    public SpamScoringAction SelectAction(Message percept)
    {
        // Sinhrono za kompatibilnost sa interfejsom
        var settings = _context.SystemSettings.First();
        
        if (settings.ActiveModelVersionId == null)
        {
            throw new InvalidOperationException("Nema aktivnog modela.");
        }

        // Dohvati pSpam
        var pSpamTask = _scoringService.ScoreMessageAsync(percept);
        pSpamTask.Wait();
        var result = pSpamTask.Result;

        return new SpamScoringAction(
            percept.Id,
            percept.Text,
            result.PSpam,
            result.Decision
        );
    }
}

/// <summary>
/// Aktuator: izvršava odluku (update statusa već urađen u ScoringService).
/// Ovdje samo vraćamo rezultat.
/// </summary>
public class SpamScoringActuator : IActuator<SpamScoringAction, SpamScoringResult>
{
    private readonly SpamAgentDbContext _context;

    public SpamScoringActuator(SpamAgentDbContext context)
    {
        _context = context;
    }

    public SpamScoringResult Execute(SpamScoringAction action)
    {
        // Status je već ažuriran u ScoringService
        var message = _context.Messages.Find(action.MessageId);
        
        return new SpamScoringResult(
            action.MessageId,
            message?.Status ?? MessageStatus.Queued,
            true
        );
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     SCORING AGENT
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Agent koji procesira queue poruka: Sense → Think → Act.
/// </summary>
public class ScoringAgent : SoftwareAgent<Message, SpamScoringAction, SpamScoringResult, object>
{
    private readonly MessageQueuePerception _perception;
    private ScoringResult? _lastResult;

    public ScoringAgent(
        MessageQueuePerception perception,
        SpamScoringPolicy policy,
        SpamScoringActuator actuator)
        : base(
            perception: perception,
            policy: policy,
            actuator: actuator,
            experienceBuilder: null,  // Nema učenja u ovom agentu
            learner: null,
            goalChecker: () => !perception.HasNext
        )
    {
        _perception = perception;
    }

    /// <summary>
    /// Da li ima još poruka za procesiranje.
    /// </summary>
    public bool HasMoreMessages => _perception.HasNext;

    /// <summary>
    /// Izvršava jedan korak i vraća rezultat.
    /// </summary>
    public SpamScoringResult? StepWithResult()
    {
        var percept = _perception.Observe();
        if (percept == null) return null;

        var action = _policy.SelectAction(percept);
        var result = _actuator.Execute(action);

        return result;
    }
}
