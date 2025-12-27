/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT WEB - ADMIN CONTROLLER
 * ═══════════════════════════════════════════════════════════════════════════════
 * 
 * API za administraciju: trening, aktivacija modela, postavke.
 * Kontroler je tanak - koristi servise iz shared library-ja.
 * 
 * NAPOMENA: Ovi endpointi nemaju autentikaciju - nisu za produkciju!
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using AiAgents.SpamAgent.Domain;
using AiAgents.SpamAgent.Infrastructure;
using AiAgents.SpamAgent.Application.Services;
using AiAgents.SpamAgent.Application.Queries;
using AiAgents.SpamAgent.Application.Agents;
using AiAgents.SpamAgent.Web.Hubs;
using AiAgents.SpamAgent.Web.Models;
using AiAgents.SpamAgent.Web.BackgroundServices;

namespace AiAgents.SpamAgent.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly SpamAgentDbContext _context;
    private readonly AdminQueryService _adminQuery;
    private readonly TrainingService _trainingService;
    private readonly DatabaseSeeder _seeder;
    private readonly RetrainAgent _retrainAgent;
    private readonly IHubContext<SpamAgentHub> _hubContext;
    private readonly SimulatorService? _simulatorService;
    private readonly SpamAgentOptions _options;

    public AdminController(
        SpamAgentDbContext context,
        AdminQueryService adminQuery,
        TrainingService trainingService,
        DatabaseSeeder seeder,
        RetrainAgent retrainAgent,
        IHubContext<SpamAgentHub> hubContext,
        SpamAgentOptions options,
        IServiceProvider serviceProvider)
    {
        _context = context;
        _adminQuery = adminQuery;
        _trainingService = trainingService;
        _seeder = seeder;
        _retrainAgent = retrainAgent;
        _hubContext = hubContext;
        _options = options;

        // SimulatorService je opcioni
        _simulatorService = serviceProvider.GetService(typeof(SimulatorService)) as SimulatorService;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //                     STATUS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Kompletni status sistema.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(SystemStatusDto), 200)]
    public async Task<ActionResult<SystemStatusDto>> GetStatus()
    {
        var status = await _adminQuery.GetSystemStatusAsync();
        return Ok(MapToDto(status));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //                     DATASET IMPORT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Importuje UCI dataset (ako nije već importovan).
    /// </summary>
    [HttpPost("import")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<ActionResult> ImportDataset([FromQuery] bool force = false)
    {
        var datasetPath = System.IO.Path.Combine(AppContext.BaseDirectory, _options.DatasetPath);
        var (imported, skipped) = await _seeder.ImportUciDatasetAsync(datasetPath, force);

        return Ok(new 
        { 
            imported, 
            skipped,
            message = imported > 0 
                ? $"Importovano {imported} poruka." 
                : $"Dataset već importovan ({skipped} poruka)."
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //                     TRENING
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Trenira novi model.
    /// </summary>
    [HttpPost("train")]
    [ProducesResponseType(typeof(ModelVersionDto), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<ModelVersionDto>> TrainModel([FromBody] TrainRequest request)
    {
        if (!Enum.TryParse<TrainTemplate>(request.Template, true, out var template))
        {
            return BadRequest("Template mora biti: Light, Medium, Full");
        }

        var result = await _retrainAgent.ForceRetrainAsync(template, request.Activate);
        
        if (!result.Success)
        {
            return BadRequest(result.Reason);
        }

        // Emituj SignalR event
        var evt = new ModelRetrainedEvent
        {
            NewVersion = result.NewModelVersion ?? 0,
            Template = result.Template?.ToString() ?? template.ToString(),
            Metrics = result.Metrics != null ? new MetricsDto
            {
                Accuracy = result.Metrics.Accuracy,
                Precision = result.Metrics.Precision,
                Recall = result.Metrics.Recall,
                F1 = result.Metrics.F1
            } : new MetricsDto(),
            IsActivated = request.Activate,
            Timestamp = DateTime.UtcNow
        };
        await _hubContext.SendModelRetrained(evt);

        var model = await _adminQuery.GetModelByVersionAsync(result.NewModelVersion ?? 0);
        return Ok(MapModelToDto(model!));
    }

    /// <summary>
    /// Forsira retrain (ignorira counter).
    /// </summary>
    [HttpPost("retrain")]
    [ProducesResponseType(typeof(ModelVersionDto), 200)]
    public async Task<ActionResult<ModelVersionDto>> ForceRetrain(
        [FromQuery] string template = "Medium",
        [FromQuery] bool activate = true)
    {
        if (!Enum.TryParse<TrainTemplate>(template, true, out var tmpl))
        {
            tmpl = TrainTemplate.Medium;
        }

        var result = await _retrainAgent.ForceRetrainAsync(tmpl, activate);

        if (!result.Success)
        {
            return BadRequest(result.Reason);
        }

        // Emituj SignalR event
        var evt = new ModelRetrainedEvent
        {
            NewVersion = result.NewModelVersion ?? 0,
            Template = result.Template?.ToString() ?? template,
            Metrics = result.Metrics != null ? new MetricsDto
            {
                Accuracy = result.Metrics.Accuracy,
                Precision = result.Metrics.Precision,
                Recall = result.Metrics.Recall,
                F1 = result.Metrics.F1
            } : new MetricsDto(),
            IsActivated = activate,
            Timestamp = DateTime.UtcNow
        };
        await _hubContext.SendModelRetrained(evt);

        var model = await _adminQuery.GetModelByVersionAsync(result.NewModelVersion ?? 0);
        return Ok(MapModelToDto(model!));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //                     MODELI
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sve verzije modela.
    /// </summary>
    [HttpGet("models")]
    [ProducesResponseType(typeof(List<ModelVersionDto>), 200)]
    public async Task<ActionResult<List<ModelVersionDto>>> GetModels()
    {
        var models = await _adminQuery.GetAllModelsAsync();
        return Ok(models.Select(MapModelToDto).ToList());
    }

    /// <summary>
    /// Aktivira postojeći model.
    /// </summary>
    [HttpPost("models/{version}/activate")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult> ActivateModel(int version)
    {
        var model = await _adminQuery.GetModelByVersionAsync(version);
        if (model == null)
        {
            return NotFound($"Model v{version} nije pronađen.");
        }

        await _trainingService.ActivateModelAsync(model.Id);

        // Emituj SignalR event
        await _hubContext.SendModelActivated(version);

        return Ok(new { message = $"Model v{version} aktiviran.", version });
    }

    /// <summary>
    /// Status aktivnog modela.
    /// </summary>
    [HttpGet("model/status")]
    [ProducesResponseType(typeof(ModelVersionDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ModelVersionDto>> GetActiveModelStatus()
    {
        var model = await _adminQuery.GetActiveModelAsync();

        if (model == null)
        {
            return NotFound("Nema aktivnog modela.");
        }

        return Ok(MapModelToDto(model));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //                     POSTAVKE
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Dohvata postavke.
    /// </summary>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(SettingsDto), 200)]
    public async Task<ActionResult<SettingsDto>> GetSettings()
    {
        var settings = await _adminQuery.GetSettingsAsync();
        return Ok(MapSettingsToDto(settings));
    }

    /// <summary>
    /// Ažurira postavke.
    /// </summary>
    [HttpPut("settings")]
    [ProducesResponseType(typeof(SettingsDto), 200)]
    public async Task<ActionResult<SettingsDto>> UpdateSettings([FromBody] SettingsRequest request)
    {
        var settings = await _context.SystemSettings.FirstAsync();

        if (request.ThresholdAllow.HasValue)
            settings.ThresholdAllow = request.ThresholdAllow.Value;
        
        if (request.ThresholdBlock.HasValue)
            settings.ThresholdBlock = request.ThresholdBlock.Value;
        
        if (request.RetrainGoldThreshold.HasValue)
            settings.RetrainGoldThreshold = request.RetrainGoldThreshold.Value;
        
        if (request.AutoRetrainEnabled.HasValue)
            settings.AutoRetrainEnabled = request.AutoRetrainEnabled.Value;

        await _context.SaveChangesAsync();

        // Simulator postavke
        if (_simulatorService != null)
        {
            if (request.SimulatorEnabled.HasValue)
                _simulatorService.SetEnabled(request.SimulatorEnabled.Value);
            
            if (request.SimulatorIntervalMs.HasValue)
                _simulatorService.SetInterval(request.SimulatorIntervalMs.Value);
            
            if (request.SimulatorBatchSize.HasValue)
                _simulatorService.SetBatchSize(request.SimulatorBatchSize.Value);
        }

        var updatedSettings = await _adminQuery.GetSettingsAsync();
        return Ok(MapSettingsToDto(updatedSettings));
    }

    /// <summary>
    /// Postavlja pragove odlučivanja.
    /// </summary>
    [HttpPut("thresholds")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<ActionResult> SetThresholds([FromBody] ThresholdsRequest request)
    {
        var settings = await _context.SystemSettings.FirstAsync();
        settings.ThresholdAllow = request.ThresholdAllow;
        settings.ThresholdBlock = request.ThresholdBlock;
        await _context.SaveChangesAsync();

        return Ok(new 
        { 
            thresholdAllow = settings.ThresholdAllow, 
            thresholdBlock = settings.ThresholdBlock 
        });
    }

    /// <summary>
    /// Uključuje/isključuje auto-retrain.
    /// </summary>
    [HttpPost("auto-retrain/{enabled}")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<ActionResult> SetAutoRetrain(bool enabled)
    {
        var settings = await _context.SystemSettings.FirstAsync();
        settings.AutoRetrainEnabled = enabled;
        await _context.SaveChangesAsync();

        return Ok(new { autoRetrainEnabled = enabled });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //                     SIMULATOR
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Status simulatora.
    /// </summary>
    [HttpGet("simulator")]
    [ProducesResponseType(typeof(object), 200)]
    public ActionResult GetSimulatorStatus()
    {
        if (_simulatorService == null)
        {
            return Ok(new { available = false });
        }

        return Ok(new 
        { 
            available = true,
            enabled = _simulatorService.IsEnabled,
            intervalMs = _simulatorService.IntervalMs,
            batchSize = _simulatorService.BatchSize
        });
    }

    /// <summary>
    /// Uključuje/isključuje simulator.
    /// </summary>
    [HttpPost("simulator/{enabled}")]
    [ProducesResponseType(typeof(object), 200)]
    public ActionResult SetSimulatorEnabled(bool enabled)
    {
        if (_simulatorService == null)
        {
            return BadRequest("Simulator nije dostupan.");
        }

        _simulatorService.SetEnabled(enabled);

        return Ok(new { enabled = _simulatorService.IsEnabled });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //                     MAPPERS
    // ═══════════════════════════════════════════════════════════════════════════

    private static SystemStatusDto MapToDto(SystemStatus status)
    {
        return new SystemStatusDto
        {
            ActiveModel = status.ActiveModel != null ? MapModelToDto(status.ActiveModel) : null,
            Settings = MapSettingsToDto(status.Settings),
            QueueStats = new QueueStatsDto
            {
                Queued = status.QueueCounts.Queued,
                InInbox = status.QueueCounts.InInbox,
                InSpam = status.QueueCounts.InSpam,
                PendingReview = status.QueueCounts.PendingReview,
                TotalProcessed = status.QueueCounts.TotalProcessed
            },
            DatasetStats = new DatasetStatsDto
            {
                TotalMessages = status.DatasetStats.TotalMessages,
                UciMessages = status.DatasetStats.UciMessages,
                RuntimeMessages = status.DatasetStats.RuntimeMessages,
                TrainPoolCount = status.DatasetStats.TrainPoolCount,
                ValidationCount = status.DatasetStats.ValidationCount,
                HamCount = status.DatasetStats.HamCount,
                SpamCount = status.DatasetStats.SpamCount,
                TotalGoldLabels = status.DatasetStats.TotalGoldLabels
            }
        };
    }

    private static ModelVersionDto MapModelToDto(ModelVersionInfo m)
    {
        return new ModelVersionDto
        {
            Id = m.Id,
            Version = m.Version,
            TrainTemplate = m.TrainTemplate.ToString(),
            TrainSetSize = m.TrainSetSize,
            GoldIncludedCount = m.GoldIncludedCount,
            ValidationSetSize = m.ValidationSetSize,
            Metrics = new MetricsDto
            {
                Accuracy = m.Metrics.Accuracy,
                Precision = m.Metrics.Precision,
                Recall = m.Metrics.Recall,
                F1 = m.Metrics.F1
            },
            ThresholdAllow = m.ThresholdAllow,
            ThresholdBlock = m.ThresholdBlock,
            IsActive = m.IsActive,
            CreatedAtUtc = m.CreatedAtUtc
        };
    }

    private static SettingsDto MapSettingsToDto(SettingsInfo s)
    {
        return new SettingsDto
        {
            ThresholdAllow = s.ThresholdAllow,
            ThresholdBlock = s.ThresholdBlock,
            RetrainGoldThreshold = s.RetrainGoldThreshold,
            NewGoldSinceLastTrain = s.NewGoldSinceLastTrain,
            AutoRetrainEnabled = s.AutoRetrainEnabled,
            LastRetrainAtUtc = s.LastRetrainAtUtc
        };
    }
}
