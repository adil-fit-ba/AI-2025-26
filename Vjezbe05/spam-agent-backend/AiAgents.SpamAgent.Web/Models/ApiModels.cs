/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT WEB - API MODELI (DTOs)
 * ═══════════════════════════════════════════════════════════════════════════════
 */

using System;
using System.Collections.Generic;
using AiAgents.SpamAgent.Domain;

namespace AiAgents.SpamAgent.Web.Models;

// ════════════════════════════════════════════════════════════════════════════════
//                     REQUEST MODELI
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Request za slanje nove poruke u queue.
/// </summary>
public class SendMessageRequest
{
    public string Text { get; set; } = string.Empty;
    public string? Source { get; set; }
}

/// <summary>
/// Request za moderatorski review.
/// </summary>
public class ReviewRequest
{
    public string Label { get; set; } = string.Empty; // "ham" ili "spam"
    public string? Note { get; set; }
    public string? ReviewedBy { get; set; }
}

/// <summary>
/// Request za trening modela.
/// </summary>
public class TrainRequest
{
    public string Template { get; set; } = "Medium"; // Light, Medium, Full
    public bool Activate { get; set; } = true;
}

/// <summary>
/// Request za postavljanje pragova.
/// </summary>
public class ThresholdsRequest
{
    public double ThresholdAllow { get; set; }
    public double ThresholdBlock { get; set; }
}

/// <summary>
/// Request za postavke.
/// </summary>
public class SettingsRequest
{
    public double? ThresholdAllow { get; set; }
    public double? ThresholdBlock { get; set; }
    public int? RetrainGoldThreshold { get; set; }
    public bool? AutoRetrainEnabled { get; set; }
    public bool? SimulatorEnabled { get; set; }
    public int? SimulatorIntervalMs { get; set; }
    public int? SimulatorBatchSize { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     RESPONSE MODELI
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Osnovna informacija o poruci.
/// </summary>
public class MessageDto
{
    public long Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TrueLabel { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public PredictionDto? LastPrediction { get; set; }
}

/// <summary>
/// Predikcija za poruku.
/// </summary>
public class PredictionDto
{
    public double PSpam { get; set; }
    public string Decision { get; set; } = string.Empty;
    public int ModelVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Informacija o verziji modela.
/// </summary>
public class ModelVersionDto
{
    public int Id { get; set; }
    public int Version { get; set; }
    public string TrainTemplate { get; set; } = string.Empty;
    public int TrainSetSize { get; set; }
    public int GoldIncludedCount { get; set; }
    public int ValidationSetSize { get; set; }
    public MetricsDto Metrics { get; set; } = new();
    public double ThresholdAllow { get; set; }
    public double ThresholdBlock { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Metrike modela.
/// </summary>
public class MetricsDto
{
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1 { get; set; }
}

/// <summary>
/// Status sistema.
/// </summary>
public class SystemStatusDto
{
    public ModelVersionDto? ActiveModel { get; set; }
    public SettingsDto Settings { get; set; } = new();
    public QueueStatsDto QueueStats { get; set; } = new();
    public DatasetStatsDto DatasetStats { get; set; } = new();
}

/// <summary>
/// Postavke sistema.
/// </summary>
public class SettingsDto
{
    public double ThresholdAllow { get; set; }
    public double ThresholdBlock { get; set; }
    public int RetrainGoldThreshold { get; set; }
    public int NewGoldSinceLastTrain { get; set; }
    public bool AutoRetrainEnabled { get; set; }
    public DateTime? LastRetrainAtUtc { get; set; }
}

/// <summary>
/// Statistika queue-a.
/// </summary>
public class QueueStatsDto
{
    public int Queued { get; set; }
    public int InInbox { get; set; }
    public int InSpam { get; set; }
    public int PendingReview { get; set; }
    public int TotalProcessed { get; set; }
}

/// <summary>
/// Statistika dataseta.
/// </summary>
public class DatasetStatsDto
{
    public int TotalMessages { get; set; }
    public int UciMessages { get; set; }
    public int RuntimeMessages { get; set; }
    public int TrainPoolCount { get; set; }
    public int ValidationCount { get; set; }
    public int HamCount { get; set; }
    public int SpamCount { get; set; }
    public int TotalGoldLabels { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     SIGNALR EVENT MODELI
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Event kad se poruka doda u queue.
/// </summary>
public class MessageQueuedEvent
{
    public long MessageId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event kad se poruka scoruje.
/// </summary>
public class MessageScoredEvent
{
    public long MessageId { get; set; }
    public string TextPreview { get; set; } = string.Empty;
    public double PSpam { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string? TrueLabel { get; set; }
    public bool? IsCorrect { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event kad se poruka premjesti (review).
/// </summary>
public class MessageMovedEvent
{
    public long MessageId { get; set; }
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string? Label { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event kad se model retrenira.
/// </summary>
public class ModelRetrainedEvent
{
    public int NewVersion { get; set; }
    public string Template { get; set; } = string.Empty;
    public MetricsDto Metrics { get; set; } = new();
    public bool IsActivated { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event sa statistikom (periodično).
/// </summary>
public class StatsUpdatedEvent
{
    public QueueStatsDto QueueStats { get; set; } = new();
    public int NewGoldSinceLastTrain { get; set; }
    public int RetrainGoldThreshold { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
