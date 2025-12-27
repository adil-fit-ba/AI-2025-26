/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - QUEUE SERVICE
 * ═══════════════════════════════════════════════════════════════════════════════
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiAgents.SpamAgent.Domain;
using AiAgents.SpamAgent.Infrastructure;

namespace AiAgents.SpamAgent.Application.Services;

// ════════════════════════════════════════════════════════════════════════════════
//                     DTO ZA ENQUEUE REZULTAT
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// DTO za enqueue-ovanu poruku - koristi SimulatorService za SignalR evente.
/// </summary>
public record QueuedMessageDto(long Id, string TextPreview, DateTime CreatedAtUtc);

// ════════════════════════════════════════════════════════════════════════════════
//                     QUEUE SERVICE
// ════════════════════════════════════════════════════════════════════════════════

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

    // ════════════════════════════════════════════════════════════════════════════
    //                     CLAIM (ATOMIČNI DEQUEUE)
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Atomično preuzima sljedeću poruku iz queue-a.
    /// 
    /// VAŽNO: Ova metoda koristi ExecuteUpdateAsync za conditional update,
    /// što osigurava da samo jedan worker može preuzeti poruku čak i ako
    /// više workera radi paralelno.
    /// 
    /// Pattern:
    ///   1. SELECT najstarija Queued poruka (samo ID)
    ///   2. UPDATE SET Status=Processing WHERE Id=X AND Status=Queued
    ///   3. Ako updated==1 → uspješno claim-ano, vrati poruku
    ///   4. Ako updated==0 → neko drugi je uzeo, retry
    /// </summary>
    public async Task<Message?> ClaimNextQueuedAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        while (true)
        {
            // 1. Pronađi kandidata (samo ID, bez trackinga)
            var candidateId = await _context.Messages
                .AsNoTracking()
                .Where(m => m.Status == MessageStatus.Queued)
                .OrderBy(m => m.CreatedAtUtc)
                .Select(m => m.Id)
                .FirstOrDefaultAsync(ct);

            if (candidateId == 0)
                return null; // Queue je prazan

            ct.ThrowIfCancellationRequested();

            // 2. Conditional update: samo jedan worker može uspjeti
            var updated = await _context.Messages
                .Where(m => m.Id == candidateId && m.Status == MessageStatus.Queued)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(m => m.Status, MessageStatus.Processing), 
                    ct);

            if (updated == 1)
            {
                // 3. Uspješno claim-ano - učitaj poruku za dalju obradu
                return await _context.Messages.FirstAsync(m => m.Id == candidateId, ct);
            }

            // 4. Neko drugi je uzeo poruku između SELECT i UPDATE → retry
            ct.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// LEGACY: Dohvata sljedeću poruku iz queue-a BEZ atomičnog claim-a.
    /// 
    /// UPOZORENJE: Ova metoda može uzrokovati race condition ako više workera
    /// radi paralelno. Koristi ClaimNextQueuedAsync za produkciju.
    /// </summary>
    [Obsolete("Koristi ClaimNextQueuedAsync za atomični dequeue. Ova metoda postoji samo za kompatibilnost.")]
    public async Task<Message?> DequeueNextAsync(CancellationToken ct = default)
    {
        return await _context.Messages
            .Where(m => m.Status == MessageStatus.Queued)
            .OrderBy(m => m.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    // ════════════════════════════════════════════════════════════════════════════
    //                     ENQUEUE
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Dodaje poruke iz validation seta u queue za procesiranje.
    /// Vraća listu DTO-a za enqueue-ovane poruke (za SignalR evente).
    /// </summary>
    /// <param name="count">Broj poruka za enqueue</param>
    /// <param name="copyAsTrueLabel">Ako true, kopira TrueLabel (za evaluaciju)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Lista DTO-a sa ID-evima i preview-ima dodanih poruka</returns>
    public async Task<IReadOnlyList<QueuedMessageDto>> EnqueueFromValidationWithResultAsync(
        int count, 
        bool copyAsTrueLabel = true,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Uzmi poruke iz ValidationHoldout koje još nisu enqueue-ane
        var candidates = await _context.Messages
            .Where(m => m.Source == MessageSource.Uci && 
                       m.Split == DataSplit.ValidationHoldout &&
                       m.Status == MessageStatus.Dataset)
            .OrderBy(m => m.Id)
            .Take(count)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            // Ako nema više, resetuj validation set (za ponovljeni demo)
            await _context.Messages
                .Where(m => m.Source == MessageSource.Uci && 
                           m.Split == DataSplit.ValidationHoldout &&
                           m.Status != MessageStatus.Dataset)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(m => m.Status, MessageStatus.Dataset), 
                    ct);

            // Pokušaj ponovo
            candidates = await _context.Messages
                .Where(m => m.Source == MessageSource.Uci && 
                           m.Split == DataSplit.ValidationHoldout &&
                           m.Status == MessageStatus.Dataset)
                .OrderBy(m => m.Id)
                .Take(count)
                .ToListAsync(ct);
        }

        if (candidates.Count == 0)
            return Array.Empty<QueuedMessageDto>();

        ct.ThrowIfCancellationRequested();

        // Kreiraj runtime kopije
        var newMessages = new List<Message>();
        var now = DateTime.UtcNow;

        foreach (var candidate in candidates)
        {
            var runtime = new Message
            {
                Source = MessageSource.Runtime,
                Text = candidate.Text,
                TrueLabel = copyAsTrueLabel ? candidate.TrueLabel : null,
                Status = MessageStatus.Queued,
                CreatedAtUtc = now
            };
            newMessages.Add(runtime);

            // Označi original kao "iskorišten" da se ne koristi ponovo
            candidate.Status = MessageStatus.Scored;
        }

        _context.Messages.AddRange(newMessages);
        await _context.SaveChangesAsync(ct);

        // Vrati DTO-e za baš te poruke (ne radi novi query!)
        return newMessages
            .Select(m => new QueuedMessageDto(
                m.Id,
                m.Text.Length > 50 ? m.Text[..50] + "..." : m.Text,
                m.CreatedAtUtc))
            .ToList();
    }

    /// <summary>
    /// Dodaje poruke iz validation seta u queue za procesiranje.
    /// </summary>
    /// <param name="count">Broj poruka za enqueue</param>
    /// <param name="copyAsTrueLabel">Ako true, kopira TrueLabel (za evaluaciju)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Broj dodanih poruka</returns>
    public async Task<int> EnqueueFromValidationAsync(
        int count, 
        bool copyAsTrueLabel = true,
        CancellationToken ct = default)
    {
        var created = await EnqueueFromValidationWithResultAsync(count, copyAsTrueLabel, ct);
        return created.Count;
    }

    // ════════════════════════════════════════════════════════════════════════════
    //                     QUERY METODE
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Vraća broj poruka u queue-u.
    /// </summary>
    public async Task<int> GetQueueCountAsync(CancellationToken ct = default)
    {
        return await _context.Messages
            .Where(m => m.Status == MessageStatus.Queued)
            .CountAsync(ct);
    }

    /// <summary>
    /// Vraća statistiku po statusima za runtime poruke.
    /// </summary>
    public async Task<Dictionary<MessageStatus, int>> GetStatusCountsAsync(CancellationToken ct = default)
    {
        return await _context.Messages
            .Where(m => m.Source == MessageSource.Runtime)
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);
    }

    /// <summary>
    /// Dodaje novu runtime poruku u queue.
    /// </summary>
    public async Task<Message> AddMessageAsync(string text, CancellationToken ct = default)
    {
        var message = new Message
        {
            Source = MessageSource.Runtime,
            Text = text,
            Status = MessageStatus.Queued,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync(ct);

        return message;
    }
}
