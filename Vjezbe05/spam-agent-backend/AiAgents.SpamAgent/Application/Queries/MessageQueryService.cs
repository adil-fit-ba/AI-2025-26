/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - MESSAGE QUERY SERVICE
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 * Read-only query servis za poruke.
 * Kontroleri koriste ovo umjesto direktnog LINQ-a.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiAgents.SpamAgent.Domain;
using AiAgents.SpamAgent.Infrastructure;

namespace AiAgents.SpamAgent.Application.Queries;

/// <summary>
/// Query servis za poruke - read-only operacije.
/// </summary>
public class MessageQueryService
{
    private readonly SpamAgentDbContext _context;

    public MessageQueryService(SpamAgentDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Dohvata nedavno procesirane poruke.
    /// </summary>
    public async Task<List<MessageDetails>> GetRecentAsync(
           int take = 50,
           MessageStatus? filterStatus = null,
           CancellationToken ct = default)
    {
        var baseQuery = _context.Messages
            .Where(m => m.Source == MessageSource.Runtime);

        if (filterStatus.HasValue)
            baseQuery = baseQuery.Where(m => m.Status == filterStatus.Value);

        return await ProjectToDetails(baseQuery)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Dohvata detalje poruke po ID-u.
    /// </summary>
    public async Task<MessageDetails?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await ProjectToDetails(_context.Messages.Where(m => m.Id == id))
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Dohvata poruke u queue-u.
    /// </summary>
    public async Task<List<MessageDetails>> GetQueuedAsync(int take = 50, CancellationToken ct = default)
    {
        return await ProjectToDetails(_context.Messages.Where(m => m.Status == MessageStatus.Queued))
            .OrderBy(m => m.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Dohvata poruke koje čekaju review.
    /// </summary>
    public async Task<List<MessageDetails>> GetPendingReviewAsync(int take = 50, CancellationToken ct = default)
    {
        return await ProjectToDetails(_context.Messages.Where(m => m.Status == MessageStatus.PendingReview))
            .OrderBy(m => m.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Vraća statistiku po statusima (samo runtime poruke).
    /// </summary>
    public async Task<QueueCounts> GetCountsAsync(CancellationToken ct = default)
    {
        var counts = await _context.Messages
            .AsNoTracking()
            .Where(m => m.Source == MessageSource.Runtime)
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

        return new QueueCounts
        {
            Queued = counts.GetValueOrDefault(MessageStatus.Queued),
            InInbox = counts.GetValueOrDefault(MessageStatus.InInbox),
            InSpam = counts.GetValueOrDefault(MessageStatus.InSpam),
            PendingReview = counts.GetValueOrDefault(MessageStatus.PendingReview)
        };
    }

    /// <summary>
    /// Broj poruka u određenom statusu.
    /// </summary>
    public Task<int> CountByStatusAsync(MessageStatus status, CancellationToken ct = default)
    {
        return _context.Messages
            .AsNoTracking()
            .Where(m => m.Status == status)
            .CountAsync(ct);
    }

    private IQueryable<MessageDetails> ProjectToDetails(IQueryable<Message> query)
    {
        return query
            .AsNoTracking()
            .Select(m => new MessageDetails
            {
                Id = m.Id,
                Text = m.Text,
                Source = m.Source,
                Status = m.Status,
                TrueLabel = m.TrueLabel,
                CreatedAtUtc = m.CreatedAtUtc,

                LastPrediction = m.Predictions
                    .OrderByDescending(p => p.CreatedAtUtc)
                    .Select(p => new PredictionDetails
                    {
                        PSpam = p.PSpam,
                        Decision = p.Decision,
                        ModelVersion = p.ModelVersion != null ? p.ModelVersion.Version : 0,
                        CreatedAtUtc = p.CreatedAtUtc
                    })
                    .FirstOrDefault()
            });
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     RESULT DTOs
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Detalji poruke sa zadnjom predikcijom.
/// </summary>
public class MessageDetails
{
    public long Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public MessageSource Source { get; set; }
    public MessageStatus Status { get; set; }
    public Label? TrueLabel { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public PredictionDetails? LastPrediction { get; set; }
}

/// <summary>
/// Detalji predikcije.
/// </summary>
public class PredictionDetails
{
    public double PSpam { get; set; }
    public SpamDecision Decision { get; set; }
    public int ModelVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Brojači po statusima.
/// </summary>
public class QueueCounts
{
    public int Queued { get; set; }
    public int InInbox { get; set; }
    public int InSpam { get; set; }
    public int PendingReview { get; set; }

    public int TotalProcessed => InInbox + InSpam + PendingReview;
}
