/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - DATABASE SEEDER (UCI Import)
 * ═══════════════════════════════════════════════════════════════════════════════
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiAgents.SpamAgent.Domain;

namespace AiAgents.SpamAgent.Infrastructure;

public class DatabaseSeeder
{
    private readonly SpamAgentDbContext _context;
    private readonly Random _rng;
    private const int SEED = 42;
    private const double TRAIN_RATIO = 0.80;

    public DatabaseSeeder(SpamAgentDbContext context)
    {
        _context = context;
        _rng = new Random(SEED);
    }

    /// <summary>
    /// Importuje UCI SMS dataset u bazu sa determinističkim splitom.
    /// </summary>
    /// <param name="datasetPath">Putanja do SMSSpamCollection fajla</param>
    /// <param name="force">Ako true, briše postojeće UCI poruke prije importa</param>
    /// <returns>Broj importovanih poruka</returns>
    public async Task<(int imported, int skipped)> ImportUciDatasetAsync(string datasetPath, bool force = false)
    {
        if (!File.Exists(datasetPath))
        {
            throw new FileNotFoundException($"Dataset nije pronađen: {datasetPath}");
        }

        // Provjeri da li je već importovano
        var existingCount = await _context.Messages
            .Where(m => m.Source == MessageSource.Uci)
            .CountAsync();

        if (existingCount > 0 && !force)
        {
            return (0, existingCount);
        }

        // Ako force, obriši postojeće UCI poruke
        if (force && existingCount > 0)
        {
            var uciMessages = await _context.Messages
                .Where(m => m.Source == MessageSource.Uci)
                .ToListAsync();
            
            _context.Messages.RemoveRange(uciMessages);
            await _context.SaveChangesAsync();
        }

        // Učitaj i parsiraj dataset
        var lines = await File.ReadAllLinesAsync(datasetPath);
        var messages = new List<Message>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Format: label\ttext
            var tabIndex = line.IndexOf('\t');
            if (tabIndex <= 0) continue;

            var labelStr = line.Substring(0, tabIndex).Trim().ToLowerInvariant();
            var text = line.Substring(tabIndex + 1).Trim();

            if (string.IsNullOrEmpty(text)) continue;

            var label = labelStr == "spam" ? Label.Spam : Label.Ham;

            messages.Add(new Message
            {
                Source = MessageSource.Uci,
                Text = text,
                TrueLabel = label,
                Status = MessageStatus.Dataset,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        // Deterministički shuffle sa fiksnim seed-om
        var shuffled = messages.OrderBy(_ => _rng.Next()).ToList();

        // Raspodjeli u TrainPool i ValidationHoldout
        var trainCount = (int)(shuffled.Count * TRAIN_RATIO);

        for (int i = 0; i < shuffled.Count; i++)
        {
            shuffled[i].Split = i < trainCount ? DataSplit.TrainPool : DataSplit.ValidationHoldout;
        }

        // Spremi u bazu
        _context.Messages.AddRange(shuffled);
        await _context.SaveChangesAsync();

        return (shuffled.Count, 0);
    }

    /// <summary>
    /// Vraća statistiku dataseta.
    /// </summary>
    public async Task<DatasetStats> GetDatasetStatsAsync()
    {
        var stats = new DatasetStats();

        stats.TotalMessages = await _context.Messages.CountAsync();
        
        stats.UciMessages = await _context.Messages
            .Where(m => m.Source == MessageSource.Uci)
            .CountAsync();
        
        stats.RuntimeMessages = await _context.Messages
            .Where(m => m.Source == MessageSource.Runtime)
            .CountAsync();

        stats.TrainPoolCount = await _context.Messages
            .Where(m => m.Split == DataSplit.TrainPool)
            .CountAsync();
        
        stats.ValidationCount = await _context.Messages
            .Where(m => m.Split == DataSplit.ValidationHoldout)
            .CountAsync();

        stats.HamCount = await _context.Messages
            .Where(m => m.TrueLabel == Label.Ham)
            .CountAsync();
        
        stats.SpamCount = await _context.Messages
            .Where(m => m.TrueLabel == Label.Spam)
            .CountAsync();

        // Status breakdown
        stats.StatusCounts = await _context.Messages
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        return stats;
    }
}

public class DatasetStats
{
    public int TotalMessages { get; set; }
    public int UciMessages { get; set; }
    public int RuntimeMessages { get; set; }
    public int TrainPoolCount { get; set; }
    public int ValidationCount { get; set; }
    public int HamCount { get; set; }
    public int SpamCount { get; set; }
    public Dictionary<MessageStatus, int> StatusCounts { get; set; } = new();
}
