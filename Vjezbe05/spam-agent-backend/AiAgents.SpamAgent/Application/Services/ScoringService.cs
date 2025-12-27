/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - SCORING SERVICE
 * ═══════════════════════════════════════════════════════════════════════════════
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiAgents.SpamAgent.Domain;
using AiAgents.SpamAgent.Infrastructure;
using AiAgents.SpamAgent.ML;

namespace AiAgents.SpamAgent.Application.Services;

/// <summary>
/// Servis za scorovanje poruka (koristi ga ScoringAgent).
/// </summary>
public class ScoringService
{
    private readonly SpamAgentDbContext _context;
    private readonly ISpamClassifier _classifier;

    public ScoringService(SpamAgentDbContext context, ISpamClassifier classifier)
    {
        _context = context;
        _classifier = classifier;
    }

    /// <summary>
    /// Scoruje poruku i ažurira njen status.
    /// </summary>
    /// <param name="message">Poruka za scorovanje</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Rezultat scorovanja</returns>
    public async Task<ScoringResult> ScoreMessageAsync(Message message, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var settings = await _context.SystemSettings
            .Include(s => s.ActiveModelVersion)
            .FirstAsync(ct);

        if (settings.ActiveModelVersionId == null)
        {
            throw new InvalidOperationException("Nema aktivnog modela. Pokrenite 'train' komandu prvo.");
        }

        if (!_classifier.IsModelLoaded)
        {
            var model = settings.ActiveModelVersion!;
            await _classifier.LoadModelAsync(model.ModelFilePath);
        }

        ct.ThrowIfCancellationRequested();

        // Izračunaj pSpam
        var pSpam = await _classifier.PredictAsync(message.Text);

        // Odredi odluku na osnovu pragova
        SpamDecision decision;
        MessageStatus newStatus;

        if (pSpam < settings.ThresholdAllow)
        {
            decision = SpamDecision.Allow;
            newStatus = MessageStatus.InInbox;
        }
        else if (pSpam >= settings.ThresholdBlock)
        {
            decision = SpamDecision.Block;
            newStatus = MessageStatus.InSpam;
        }
        else
        {
            decision = SpamDecision.PendingReview;
            newStatus = MessageStatus.PendingReview;
        }

        ct.ThrowIfCancellationRequested();

        // Kreiraj Prediction zapis
        var prediction = new Prediction
        {
            MessageId = message.Id,
            ModelVersionId = settings.ActiveModelVersionId.Value,
            PSpam = pSpam,
            Decision = decision,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Predictions.Add(prediction);

        // Ažuriraj poruku
        message.Status = newStatus;
        message.LastModelVersionId = settings.ActiveModelVersionId.Value;

        await _context.SaveChangesAsync(ct);

        return new ScoringResult
        {
            MessageId = message.Id,
            Text = message.Text,
            PSpam = pSpam,
            Decision = decision,
            NewStatus = newStatus,
            TrueLabel = message.TrueLabel
        };
    }

    /// <summary>
    /// Provjerava da li je model spreman za scorovanje.
    /// </summary>
    public async Task<bool> IsReadyAsync(CancellationToken ct = default)
    {
        var settings = await _context.SystemSettings.FirstAsync(ct);
        return settings.ActiveModelVersionId != null;
    }
}

/// <summary>
/// Rezultat scorovanja poruke.
/// </summary>
public class ScoringResult
{
    public long MessageId { get; set; }
    public string Text { get; set; } = string.Empty;
    public double PSpam { get; set; }
    public SpamDecision Decision { get; set; }
    public MessageStatus NewStatus { get; set; }
    public Label? TrueLabel { get; set; }

    /// <summary>
    /// Da li je odluka bila tačna (ako znamo TrueLabel).
    /// </summary>
    public bool? IsCorrect
    {
        get
        {
            if (TrueLabel == null) return null;
            
            // Allow/Block su "sigurne" odluke
            if (Decision == SpamDecision.Allow)
                return TrueLabel == Label.Ham;
            if (Decision == SpamDecision.Block)
                return TrueLabel == Label.Spam;
            
            // PendingReview je "nesigurna" - ne računamo kao tačno/netačno
            return null;
        }
    }
}
