/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT WEB - REVIEW CONTROLLER
 * ═══════════════════════════════════════════════════════════════════════════════
 * 
 * API za moderatorske review-e (gold labels).
 * Kontroler je tanak - koristi servise iz shared library-ja.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using AiAgents.SpamAgent.Domain;
using AiAgents.SpamAgent.Application.Services;
using AiAgents.SpamAgent.Application.Queries;
using AiAgents.SpamAgent.Web.Hubs;
using AiAgents.SpamAgent.Web.Models;

namespace AiAgents.SpamAgent.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReviewController : ControllerBase
{
    private readonly ReviewService _reviewService;
    private readonly MessageQueryService _messageQuery;
    private readonly AdminQueryService _adminQuery;
    private readonly IHubContext<SpamAgentHub> _hubContext;

    public ReviewController(
        ReviewService reviewService,
        MessageQueryService messageQuery,
        AdminQueryService adminQuery,
        IHubContext<SpamAgentHub> hubContext)
    {
        _reviewService = reviewService;
        _messageQuery = messageQuery;
        _adminQuery = adminQuery;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Dohvata poruke koje čekaju review.
    /// </summary>
    [HttpGet("queue")]
    [ProducesResponseType(typeof(List<MessageDto>), 200)]
    public async Task<ActionResult<List<MessageDto>>> GetReviewQueue([FromQuery] int take = 50)
    {
        var messages = await _messageQuery.GetPendingReviewAsync(take);
        return Ok(messages.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Broj poruka koje čekaju review.
    /// </summary>
    [HttpGet("count")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<ActionResult> GetPendingCount()
    {
        var count = await _messageQuery.CountByStatusAsync(MessageStatus.PendingReview);
        return Ok(new { pendingCount = count });
    }

    /// <summary>
    /// Dodaje moderatorski review (gold label).
    /// </summary>
    [HttpPost("{messageId}")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult> AddReview(long messageId, [FromBody] ReviewRequest request)
    {
        // Validiraj labelu
        if (!Enum.TryParse<Label>(request.Label, true, out var label))
        {
            return BadRequest("Label mora biti 'ham' ili 'spam'.");
        }

        // Dohvati poruku za provjeru starog statusa
        var messageBefore = await _messageQuery.GetByIdAsync(messageId);
        if (messageBefore == null)
        {
            return NotFound("Poruka nije pronađena.");
        }

        var oldStatus = messageBefore.Status;

        // Dodaj review
        var (success, resultMessage) = await _reviewService.AddReviewAsync(
            messageId, 
            label, 
            request.ReviewedBy ?? "web-moderator",
            request.Note);

        if (!success)
        {
            return BadRequest(resultMessage);
        }

        // Dohvati ažuriranu poruku
        var messageAfter = await _messageQuery.GetByIdAsync(messageId);

        // Emituj MessageMoved event
        var movedEvt = new MessageMovedEvent
        {
            MessageId = messageId,
            OldStatus = oldStatus.ToString(),
            NewStatus = messageAfter!.Status.ToString(),
            Label = label.ToString(),
            Timestamp = DateTime.UtcNow
        };
        await _hubContext.SendMessageMoved(movedEvt);

        // Emituj stats update
        var counts = await _messageQuery.GetCountsAsync();
        var goldStats = await _adminQuery.GetGoldStatsAsync();
        
        var statsEvt = new StatsUpdatedEvent
        {
            QueueStats = new QueueStatsDto
            {
                Queued = counts.Queued,
                InInbox = counts.InInbox,
                InSpam = counts.InSpam,
                PendingReview = counts.PendingReview,
                TotalProcessed = counts.TotalProcessed
            },
            NewGoldSinceLastTrain = goldStats.NewGoldSinceLastTrain,
            RetrainGoldThreshold = goldStats.RetrainGoldThreshold,
            Timestamp = DateTime.UtcNow
        };
        await _hubContext.SendStatsUpdated(statsEvt);

        return Ok(new 
        { 
            success = true, 
            message = resultMessage,
            newStatus = messageAfter.Status.ToString(),
            goldProgress = new 
            { 
                current = goldStats.NewGoldSinceLastTrain, 
                threshold = goldStats.RetrainGoldThreshold,
                willRetrain = goldStats.WillTriggerRetrain
            }
        });
    }

    /// <summary>
    /// Statistika gold labela.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<ActionResult> GetReviewStats()
    {
        var stats = await _adminQuery.GetGoldStatsAsync();

        return Ok(new
        {
            totalGoldLabels = stats.TotalGoldLabels,
            pendingReviewCount = stats.PendingReviewCount,
            goldProgress = new
            {
                current = stats.NewGoldSinceLastTrain,
                threshold = stats.RetrainGoldThreshold,
                percentage = stats.ProgressPercentage,
                willRetrain = stats.WillTriggerRetrain
            }
        });
    }

    private static MessageDto MapToDto(MessageDetails m)
    {
        return new MessageDto
        {
            Id = m.Id,
            Text = m.Text,
            Source = m.Source.ToString(),
            Status = m.Status.ToString(),
            TrueLabel = m.TrueLabel?.ToString(),
            CreatedAtUtc = m.CreatedAtUtc,
            LastPrediction = m.LastPrediction != null ? new PredictionDto
            {
                PSpam = m.LastPrediction.PSpam,
                Decision = m.LastPrediction.Decision.ToString(),
                ModelVersion = m.LastPrediction.ModelVersion,
                CreatedAtUtc = m.LastPrediction.CreatedAtUtc
            } : null
        };
    }
}
