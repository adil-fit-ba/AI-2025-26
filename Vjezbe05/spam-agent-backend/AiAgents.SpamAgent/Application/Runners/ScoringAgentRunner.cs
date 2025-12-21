/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - SCORING AGENT RUNNER
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 * Async runner koji implementira agent ciklus: Sense → Think → Act
 *
 *   SENSE:  Dohvati sljedeću poruku iz queue-a (Status=Queued)
 *   THINK:  ML model izračuna pSpam vjerovatnoću
 *   ACT:    Odredi odluku + update status poruke + persist prediction
 *
 * Ovaj runner se koristi iz BackgroundService-a u Web sloju.
 * Agent klase (ScoringAgent) ostaju kao edukativni primjer generičkog agenta.
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using AiAgents.SpamAgent.Domain;
using AiAgents.SpamAgent.Application.Services;

namespace AiAgents.SpamAgent.Application.Runners;

/// <summary>
/// Async runner za scoring agent - jedan tick procesira jednu poruku.
/// </summary>
public class ScoringAgentRunner
{
    private readonly QueueService _queueService;
    private readonly ScoringService _scoringService;

    public ScoringAgentRunner(
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
        // SENSE: Dohvati sljedeću poruku iz queue-a
        // ═══════════════════════════════════════════════════════════════════
        //
        // NAPOMENA (VAŽNO ZA ISPRAVNOST):
        // DequeueNextAsync treba "claimati" poruku (npr. promijeniti status sa Queued)
        // unutar transakcije, da dva workera ne uzmu istu poruku.
        //
        var message = await _queueService.DequeueNextAsync();

        ct.ThrowIfCancellationRequested();

        if (message == null)
        {
            return null; // Queue je prazan
        }

        // ═══════════════════════════════════════════════════════════════════
        // THINK + ACT: Score poruku i ažuriraj status
        // ═══════════════════════════════════════════════════════════════════
        var scoringResult = await _scoringService.ScoreMessageAsync(message);

        ct.ThrowIfCancellationRequested();

        // Mapiraj u tick result (Web sloj samo emituje SignalR event)
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
    public async Task<bool> IsReadyAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await _scoringService.IsReadyAsync();
    }

    /// <summary>
    /// Provjerava da li ima poruka za procesiranje.
    /// </summary>
    public async Task<bool> HasWorkAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await _queueService.GetQueueCountAsync() > 0;
    }
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
