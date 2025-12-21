/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT WEB - MESSAGES CONTROLLER
 * ═══════════════════════════════════════════════════════════════════════════════
 * 
 * Public API za slanje i praćenje poruka.
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
public class MessagesController : ControllerBase
{
    private readonly QueueService _queueService;
    private readonly MessageQueryService _queryService;
    private readonly IHubContext<SpamAgentHub> _hubContext;

    public MessagesController(
        QueueService queueService,
        MessageQueryService queryService,
        IHubContext<SpamAgentHub> hubContext)
    {
        _queueService = queueService;
        _queryService = queryService;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Šalje novu poruku u queue za procesiranje.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MessageDto), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<MessageDto>> SendMessage([FromBody] SendMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("Text je obavezan.");
        }

        var message = await _queueService.AddMessageAsync(request.Text);

        // Emituj SignalR event
        var evt = new MessageQueuedEvent
        {
            MessageId = message.Id,
            Text = message.Text.Length > 50 
                ? message.Text.Substring(0, 50) + "..." 
                : message.Text,
            Timestamp = DateTime.UtcNow
        };
        await _hubContext.SendMessageQueued(evt);

        // Dohvati puni DTO
        var details = await _queryService.GetByIdAsync(message.Id);
        var dto = MapToDto(details!);
        
        return CreatedAtAction(nameof(GetMessage), new { id = message.Id }, dto);
    }

    /// <summary>
    /// Dohvata poruku po ID-u sa zadnjom predikcijom.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(MessageDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<MessageDto>> GetMessage(long id)
    {
        var details = await _queryService.GetByIdAsync(id);

        if (details == null)
        {
            return NotFound();
        }

        return Ok(MapToDto(details));
    }

    /// <summary>
    /// Dohvata nedavno procesirane poruke.
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(List<MessageDto>), 200)]
    public async Task<ActionResult<List<MessageDto>>> GetRecentMessages(
        [FromQuery] int take = 50,
        [FromQuery] string? status = null)
    {
        MessageStatus? filterStatus = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<MessageStatus>(status, true, out var s))
        {
            filterStatus = s;
        }

        var messages = await _queryService.GetRecentAsync(take, filterStatus);
        return Ok(messages.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Dohvata poruke u queue-u (čekaju procesiranje).
    /// </summary>
    [HttpGet("queued")]
    [ProducesResponseType(typeof(List<MessageDto>), 200)]
    public async Task<ActionResult<List<MessageDto>>> GetQueuedMessages([FromQuery] int take = 50)
    {
        var messages = await _queryService.GetQueuedAsync(take);
        return Ok(messages.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Dodaje batch poruka iz validation seta (za demo).
    /// </summary>
    [HttpPost("enqueue")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<ActionResult> EnqueueFromValidation([FromQuery] int count = 10)
    {
        var enqueued = await _queueService.EnqueueFromValidationAsync(count, copyAsTrueLabel: true);
        return Ok(new { enqueued });
    }

    /// <summary>
    /// Statistika po statusima.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(QueueStatsDto), 200)]
    public async Task<ActionResult<QueueStatsDto>> GetStats()
    {
        var counts = await _queryService.GetCountsAsync();

        return Ok(new QueueStatsDto
        {
            Queued = counts.Queued,
            InInbox = counts.InInbox,
            InSpam = counts.InSpam,
            PendingReview = counts.PendingReview,
            TotalProcessed = counts.TotalProcessed
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
