/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - SCORING AGENT (PRODUKCIJSKI)
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 * Produkcijski agent za scoring SMS poruka.
 * 
 * Implementira Sense → Think → Act ciklus kroz async TickAsync():
 *
 *   SENSE:  Atomični claim sljedeće poruke iz queue-a (Status=Queued → Processing)
 *   THINK:  ML model izračuna pSpam vjerovatnoću
 *   ACT:    Odluka (Allow/Pending/Block) + update status poruke + persist prediction
 *
 * NAPOMENA:
 *   - Learn se ne dešava u ovom agentu - to radi RetrainAgent!
 *   - Koristi ClaimNextQueuedAsync za atomični dequeue (bez race condition)
 *   - Sva logika je async, bez .Wait() ili .Result
 *
 * RAZLIKA OD DEMO VERZIJE:
 *   - Demo verzija (DemoAgents/ScoringAgentDemo.cs) koristi SoftwareAgent<T> baznu klasu
 *   - Ova verzija implementira IProductionAgent<T> za BackgroundService integraciju
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using AiAgents.SpamAgent.Abstractions;
using AiAgents.SpamAgent.Application.Services;
using AiAgents.SpamAgent.Domain;

namespace AiAgents.SpamAgent.Application.Agents;

/// <summary>
/// Produkcijski scoring agent - jedan tick procesira jednu poruku.
/// </summary>
public sealed class ScoringAgent : IProductionAgent<ScoringTickResult>
{
    private readonly QueueService _queueService;
    private readonly ScoringService _scoringService;

    public ScoringAgent(
        QueueService queueService,
        ScoringService scoringService)
    {
        _queueService = queueService;
        _scoringService = scoringService;
    }

    /// <summary>
    /// Izvršava jedan tick agent ciklusa: Sense → Think → Act.
    /// Vraća rezultat ako je poruka procesirana, null ako je queue prazan.
    /// </summary>
    public async Task<ScoringTickResult?> TickAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ═══════════════════════════════════════════════════════════════════
        // SENSE: Atomični claim poruke iz queue-a
        // ═══════════════════════════════════════════════════════════════════
        //
        // ClaimNextQueuedAsync koristi conditional UPDATE:
        //   UPDATE Messages SET Status='Processing' 
        //   WHERE Id=X AND Status='Queued'
        //
        // Samo jedan worker može uspješno claim-ati poruku.
        //
        var message = await _queueService.ClaimNextQueuedAsync(ct);

        if (message == null)
        {
            return null; // Queue je prazan
        }

        ct.ThrowIfCancellationRequested();

        // ═══════════════════════════════════════════════════════════════════
        // THINK + ACT: Score poruku i ažuriraj status
        // ═══════════════════════════════════════════════════════════════════
        //
        // ScoringService:
        //   1. Učitava ML model ako nije učitan
        //   2. Izračunava pSpam
        //   3. Primjenjuje pragove za odluku
        //   4. Kreira Prediction zapis
        //   5. Ažurira Message status
        //
        var scoringResult = await _scoringService.ScoreMessageAsync(message, ct);

        ct.ThrowIfCancellationRequested();

        // Mapiraj u tick result (Web sloj emituje SignalR event)
        return new ScoringTickResult
        {
            MessageId = scoringResult.MessageId,
            Text = scoringResult.Text,
            PSpam = scoringResult.PSpam,
            Decision = scoringResult.Decision,
            NewStatus = scoringResult.NewStatus,
            TrueLabel = scoringResult.TrueLabel,
            IsCorrect = scoringResult.IsCorrect,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Provjerava da li je agent spreman za rad (ima aktivni model).
    /// </summary>
    public Task<bool> IsReadyAsync(CancellationToken ct = default)
        => _scoringService.IsReadyAsync(ct);
}

// ════════════════════════════════════════════════════════════════════════════════
//                     RESULT DTO
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Rezultat jednog tick-a scoring agenta.
/// Web sloj koristi ovo za SignalR evente bez dodatne logike.
/// </summary>
public class ScoringTickResult
{
    public long MessageId { get; set; }
    public string Text { get; set; } = string.Empty;
    public double PSpam { get; set; }
    public SpamDecision Decision { get; set; }
    public MessageStatus NewStatus { get; set; }
    public Label? TrueLabel { get; set; }
    public bool? IsCorrect { get; set; }
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Skraćeni tekst za prikaz (max 50 karaktera).
    /// </summary>
    public string TextPreview => string.IsNullOrEmpty(Text)
        ? string.Empty
        : (Text.Length > 50 ? Text[..50] + "..." : Text);
}
