/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - QUEUE SERVICE
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
/// Servis za upravljanje queue-om poruka.
/// </summary>
public class QueueService
{
    private readonly SpamAgentDbContext _context;

    public QueueService(SpamAgentDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Dodaje poruke iz validation seta u queue za procesiranje.
    /// </summary>
    /// <param name="count">Broj poruka za enqueue</param>
    /// <param name="copyAsTrueLabel">Ako true, kopira TrueLabel (za evaluaciju)</param>
    /// <returns>Broj dodanih poruka</returns>
    public async Task<int> EnqueueFromValidationAsync(int count, bool copyAsTrueLabel = true)
    {
        // Uzmi poruke iz ValidationHoldout koje još nisu enqueue-ane
        var candidates = await _context.Messages
            .Where(m => m.Source == MessageSource.Uci && 
                       m.Split == DataSplit.ValidationHoldout &&
                       m.Status == MessageStatus.Dataset)
            .OrderBy(m => m.Id)
            .Take(count)
            .ToListAsync();

        if (candidates.Count == 0)
        {
            // Ako nema više, resetuj validation set (za ponovljeni demo)
            var used = await _context.Messages
                .Where(m => m.Source == MessageSource.Uci && 
                           m.Split == DataSplit.ValidationHoldout &&
                           m.Status != MessageStatus.Dataset)
                .ToListAsync();

            foreach (var msg in used)
            {
                msg.Status = MessageStatus.Dataset;
            }
            await _context.SaveChangesAsync();

            // Pokušaj ponovo
            candidates = await _context.Messages
                .Where(m => m.Source == MessageSource.Uci && 
                           m.Split == DataSplit.ValidationHoldout &&
                           m.Status == MessageStatus.Dataset)
                .OrderBy(m => m.Id)
                .Take(count)
                .ToListAsync();
        }

        // Kreiraj runtime kopije
        var newMessages = new List<Message>();
        foreach (var candidate in candidates)
        {
            var runtime = new Message
            {
                Source = MessageSource.Runtime,
                Text = candidate.Text,
                TrueLabel = copyAsTrueLabel ? candidate.TrueLabel : null,
                Status = MessageStatus.Queued,
                CreatedAtUtc = DateTime.UtcNow
            };
            newMessages.Add(runtime);

            // Označi original kao "iskorišten" da se ne koristi ponovo
            candidate.Status = MessageStatus.Scored;
        }

        _context.Messages.AddRange(newMessages);
        await _context.SaveChangesAsync();

        return newMessages.Count;
    }

    /// <summary>
    /// Dohvata sljedeću poruku iz queue-a.
    /// </summary>
    public async Task<Message?> DequeueNextAsync()
    {
        return await _context.Messages
            .Where(m => m.Status == MessageStatus.Queued)
            .OrderBy(m => m.CreatedAtUtc)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Vraća broj poruka u queue-u.
    /// </summary>
    public async Task<int> GetQueueCountAsync()
    {
        return await _context.Messages
            .Where(m => m.Status == MessageStatus.Queued)
            .CountAsync();
    }

    /// <summary>
    /// Vraća statistiku po statusima.
    /// </summary>
    public async Task<Dictionary<MessageStatus, int>> GetStatusCountsAsync()
    {
        return await _context.Messages
            .Where(m => m.Source == MessageSource.Runtime)
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);
    }

    /// <summary>
    /// Dodaje novu runtime poruku u queue.
    /// </summary>
    public async Task<Message> AddMessageAsync(string text)
    {
        var message = new Message
        {
            Source = MessageSource.Runtime,
            Text = text,
            Status = MessageStatus.Queued,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        return message;
    }
}
