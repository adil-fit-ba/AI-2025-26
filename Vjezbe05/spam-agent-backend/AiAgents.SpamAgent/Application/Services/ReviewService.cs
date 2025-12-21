/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - REVIEW SERVICE
 * ═══════════════════════════════════════════════════════════════════════════════
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiAgents.SpamAgent.Domain;
using AiAgents.SpamAgent.Infrastructure;

namespace AiAgents.SpamAgent.Application.Services;

/// <summary>
/// Servis za moderatorske review-e (gold labels).
/// </summary>
public class ReviewService
{
    private readonly SpamAgentDbContext _context;

    public ReviewService(SpamAgentDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Dohvata poruke koje čekaju review.
    /// </summary>
    /// <param name="take">Maksimalan broj</param>
    public async Task<List<Message>> GetPendingReviewsAsync(int take = 10)
    {
        return await _context.Messages
            .Include(m => m.Predictions.OrderByDescending(p => p.CreatedAtUtc).Take(1))
            .Where(m => m.Status == MessageStatus.PendingReview)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(take)
            .ToListAsync();
    }

    /// <summary>
    /// Broj poruka koje čekaju review.
    /// </summary>
    public async Task<int> GetPendingCountAsync()
    {
        return await _context.Messages
            .Where(m => m.Status == MessageStatus.PendingReview)
            .CountAsync();
    }

    /// <summary>
    /// Dodaje review za poruku i ažurira status.
    /// </summary>
    /// <param name="messageId">ID poruke</param>
    /// <param name="label">Ham ili Spam</param>
    /// <param name="reviewedBy">Ko je napravio review</param>
    /// <param name="note">Opcionalna napomena</param>
    /// <returns>True ako je uspješno, false ako poruka nije pronađena ili već ima review</returns>
    public async Task<(bool success, string message)> AddReviewAsync(
        long messageId, 
        Label label, 
        string reviewedBy = "console-admin",
        string? note = null)
    {
        var msg = await _context.Messages
            .Include(m => m.Review)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (msg == null)
        {
            return (false, "Poruka nije pronađena.");
        }

        if (msg.Review != null)
        {
            return (false, "Poruka već ima review.");
        }

        // Kreiraj review
        var review = new Review
        {
            MessageId = messageId,
            Label = label,
            ReviewedBy = reviewedBy,
            ReviewedAtUtc = DateTime.UtcNow,
            Note = note
        };

        _context.Reviews.Add(review);

        // Ažuriraj TrueLabel na poruci (za runtime poruke)
        msg.TrueLabel = label;

        // Premjesti poruku u odgovarajući folder
        msg.Status = label == Label.Ham ? MessageStatus.InInbox : MessageStatus.InSpam;

        // Inkrementiraj gold counter
        var settings = await _context.SystemSettings.FirstAsync();
        settings.NewGoldSinceLastTrain++;

        await _context.SaveChangesAsync();

        return (true, $"Review dodan. Novi gold count: {settings.NewGoldSinceLastTrain}");
    }

    /// <summary>
    /// Provjerava da li treba pokrenuti auto-retrain.
    /// </summary>
    /// <returns>(shouldRetrain, currentGoldCount, threshold)</returns>
    public async Task<(bool shouldRetrain, int currentGold, int threshold)> CheckAutoRetrainAsync()
    {
        var settings = await _context.SystemSettings.FirstAsync();
        
        var shouldRetrain = settings.AutoRetrainEnabled && 
                           settings.NewGoldSinceLastTrain >= settings.RetrainGoldThreshold;

        return (shouldRetrain, settings.NewGoldSinceLastTrain, settings.RetrainGoldThreshold);
    }

    /// <summary>
    /// Vraća ukupan broj gold labela (reviews).
    /// </summary>
    public async Task<int> GetTotalGoldCountAsync()
    {
        return await _context.Reviews.CountAsync();
    }
}
