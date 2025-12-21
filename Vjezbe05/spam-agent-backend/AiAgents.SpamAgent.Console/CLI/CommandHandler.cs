/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT CONSOLE - COMMAND HANDLER
 * ═══════════════════════════════════════════════════════════════════════════════
 */

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiAgents.SpamAgent.Domain;
using AiAgents.SpamAgent.Infrastructure;
using AiAgents.SpamAgent.Application.Services;
using AiAgents.SpamAgent.ML;
using AiAgents.SpamAgent.Agent;

namespace AiAgents.SpamAgent.Console.CLI;

public class CommandHandler
{
    private readonly SpamAgentDbContext _context;
    private readonly DatabaseSeeder _seeder;
    private readonly QueueService _queueService;
    private readonly ReviewService _reviewService;
    private readonly TrainingService _trainingService;
    private readonly ScoringService _scoringService;
    private readonly ISpamClassifier _classifier;
    private readonly string _datasetPath;

    public CommandHandler(
        SpamAgentDbContext context,
        DatabaseSeeder seeder,
        QueueService queueService,
        ReviewService reviewService,
        TrainingService trainingService,
        ScoringService scoringService,
        ISpamClassifier classifier,
        string datasetPath)
    {
        _context = context;
        _seeder = seeder;
        _queueService = queueService;
        _reviewService = reviewService;
        _trainingService = trainingService;
        _scoringService = scoringService;
        _classifier = classifier;
        _datasetPath = datasetPath;
    }

    public async Task HandleAsync(string input)
    {
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var command = parts[0].ToLowerInvariant();
        var args = parts.Skip(1).ToArray();

        try
        {
            switch (command)
            {
                case "status":
                    await HandleStatusAsync();
                    break;
                case "help":
                    ConsoleUI.WriteHelp();
                    break;
                case "import":
                    await HandleImportAsync(args);
                    break;
                case "train":
                    await HandleTrainAsync(args);
                    break;
                case "activate":
                    await HandleActivateAsync(args);
                    break;
                case "models":
                    await HandleModelsAsync();
                    break;
                case "enqueue":
                    await HandleEnqueueAsync(args);
                    break;
                case "run":
                    await HandleRunAsync(args);
                    break;
                case "add":
                    await HandleAddAsync(args);
                    break;
                case "review":
                    await HandleReviewAsync(args);
                    break;
                case "set":
                    await HandleSetAsync(args);
                    break;
                case "toggle":
                    await HandleToggleAsync(args);
                    break;
                default:
                    ConsoleUI.WriteError($"Nepoznata komanda: {command}. Unesite 'help' za pomoć.");
                    break;
            }
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteError($"Greška: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //                     KOMANDE
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task HandleStatusAsync()
    {
        ConsoleUI.WriteHeader("SPAM AGENT STATUS");

        // Dataset stats
        var stats = await _seeder.GetDatasetStatsAsync();
        ConsoleUI.WriteSubHeader("Dataset");
        System.Console.WriteLine($"  UCI poruke:    {stats.UciMessages}");
        System.Console.WriteLine($"  Runtime:       {stats.RuntimeMessages}");
        System.Console.WriteLine($"  Train pool:    {stats.TrainPoolCount}");
        System.Console.WriteLine($"  Validation:    {stats.ValidationCount}");
        System.Console.WriteLine($"  Ham/Spam:      {stats.HamCount}/{stats.SpamCount}");

        // Active model
        var activeModel = await _trainingService.GetActiveModelAsync();
        ConsoleUI.WriteSubHeader("Aktivni Model");
        if (activeModel != null)
        {
            System.Console.WriteLine($"  Verzija:       v{activeModel.Version}");
            System.Console.WriteLine($"  Template:      {activeModel.TrainTemplate}");
            System.Console.WriteLine($"  Train size:    {activeModel.TrainSetSize} (+{activeModel.GoldIncludedCount} gold)");
            ConsoleUI.WriteMetrics(activeModel.Accuracy, activeModel.Precision, activeModel.Recall, activeModel.F1);
        }
        else
        {
            ConsoleUI.WriteWarning("Nema aktivnog modela. Pokrenite 'train' komandu.");
        }

        // Settings
        var settings = await _context.SystemSettings.FirstAsync();
        ConsoleUI.WriteSubHeader("Postavke");
        System.Console.WriteLine($"  Threshold Allow:  {settings.ThresholdAllow:F2}");
        System.Console.WriteLine($"  Threshold Block:  {settings.ThresholdBlock:F2}");
        System.Console.WriteLine($"  Auto-retrain:     {(settings.AutoRetrainEnabled ? "ON" : "OFF")}");
        System.Console.WriteLine($"  Retrain threshold:{settings.RetrainGoldThreshold}");
        System.Console.WriteLine($"  New gold labels:  {settings.NewGoldSinceLastTrain}/{settings.RetrainGoldThreshold}");

        // Queue status
        var statusCounts = await _queueService.GetStatusCountsAsync();
        ConsoleUI.WriteSubHeader("Runtime Poruke");
        foreach (var (status, count) in statusCounts.OrderBy(x => x.Key))
        {
            System.Console.WriteLine($"  {status,-15} {count}");
        }
    }

    private async Task HandleImportAsync(string[] args)
    {
        var force = args.Contains("--force");
        
        ConsoleUI.WriteInfo($"Importujem UCI dataset iz: {_datasetPath}");
        
        var (imported, skipped) = await _seeder.ImportUciDatasetAsync(_datasetPath, force);
        
        if (imported > 0)
        {
            ConsoleUI.WriteSuccess($"Importovano {imported} poruka.");
        }
        else if (skipped > 0)
        {
            ConsoleUI.WriteWarning($"Dataset već importovan ({skipped} poruka). Koristite --force za reimport.");
        }
    }

    private async Task HandleTrainAsync(string[] args)
    {
        if (args.Length == 0)
        {
            ConsoleUI.WriteError("Navedite template: train <light|medium|full> [--activate]");
            return;
        }

        if (!Enum.TryParse<TrainTemplate>(args[0], true, out var template))
        {
            ConsoleUI.WriteError("Nepoznat template. Koristite: light, medium, full");
            return;
        }

        var activate = args.Contains("--activate");

        ConsoleUI.WriteInfo($"Treniram model sa template: {template}...");

        var model = await _trainingService.TrainModelAsync(template, activate);

        ConsoleUI.WriteSuccess($"Model v{model.Version} treniran!");
        ConsoleUI.WriteMetrics(model.Accuracy, model.Precision, model.Recall, model.F1);

        if (activate)
        {
            ConsoleUI.WriteSuccess("Model aktiviran.");
        }
        else
        {
            ConsoleUI.WriteInfo($"Za aktivaciju: activate {model.Version}");
        }
    }

    private async Task HandleActivateAsync(string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out var version))
        {
            ConsoleUI.WriteError("Navedite verziju: activate <version>");
            return;
        }

        var model = await _context.ModelVersions.FirstOrDefaultAsync(m => m.Version == version);
        if (model == null)
        {
            ConsoleUI.WriteError($"Model v{version} nije pronađen.");
            return;
        }

        await _trainingService.ActivateModelAsync(model.Id);
        ConsoleUI.WriteSuccess($"Model v{version} aktiviran.");
    }

    private async Task HandleModelsAsync()
    {
        var models = await _trainingService.GetAllModelsAsync();
        
        if (models.Count == 0)
        {
            ConsoleUI.WriteWarning("Nema treniranih modela.");
            return;
        }

        ConsoleUI.WriteSubHeader("Verzije Modela");

        var headers = new[] { "V", "Active", "Template", "Train", "Gold", "Accuracy", "F1", "Created" };
        var rows = models.Select(m => new[]
        {
            m.Version.ToString(),
            m.IsActive ? "*" : "",
            m.TrainTemplate.ToString(),
            m.TrainSetSize.ToString(),
            m.GoldIncludedCount.ToString(),
            m.Accuracy.ToString("P1"),
            m.F1.ToString("P1"),
            m.CreatedAtUtc.ToString("MM-dd HH:mm")
        }).ToArray();

        ConsoleUI.WriteTable(headers, rows);
    }

    private async Task HandleEnqueueAsync(string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out var count))
        {
            ConsoleUI.WriteError("Navedite broj: enqueue <count>");
            return;
        }

        var enqueued = await _queueService.EnqueueFromValidationAsync(count);
        ConsoleUI.WriteSuccess($"Dodano {enqueued} poruka u queue.");
    }

    private async Task HandleRunAsync(string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out var steps))
        {
            ConsoleUI.WriteError("Navedite broj koraka: run <steps>");
            return;
        }

        if (!await _scoringService.IsReadyAsync())
        {
            ConsoleUI.WriteError("Nema aktivnog modela. Pokrenite 'train' komandu prvo.");
            return;
        }

        ConsoleUI.WriteSubHeader($"Procesiram {steps} poruka");

        int processed = 0;
        int correct = 0, incorrect = 0, pending = 0;

        for (int i = 0; i < steps; i++)
        {
            var message = await _queueService.DequeueNextAsync();
            if (message == null)
            {
                ConsoleUI.WriteWarning("Queue prazan.");
                break;
            }

            var result = await _scoringService.ScoreMessageAsync(message);
            processed++;

            // Ispis
            System.Console.Write($"\n  [{processed}] ");
            ConsoleUI.WriteMessagePreview(result.Text, 40);
            System.Console.WriteLine();
            ConsoleUI.WriteDecision(result.Decision, result.PSpam);

            // Statistika
            if (result.IsCorrect == true) correct++;
            else if (result.IsCorrect == false) incorrect++;
            else pending++;

            // Prikaži true label ako je poznat
            if (result.TrueLabel != null)
            {
                var labelColor = result.TrueLabel == Label.Spam ? ConsoleColor.Red : ConsoleColor.Green;
                System.Console.ForegroundColor = labelColor;
                System.Console.Write($" (True: {result.TrueLabel})");
                System.Console.ResetColor();
            }
            System.Console.WriteLine();
        }

        System.Console.WriteLine();
        ConsoleUI.WriteSubHeader("Rezultat");
        System.Console.WriteLine($"  Procesirano: {processed}");
        System.Console.WriteLine($"  Tačno:       {correct}");
        System.Console.WriteLine($"  Netačno:     {incorrect}");
        System.Console.WriteLine($"  Pending:     {pending}");
        
        if (correct + incorrect > 0)
        {
            var accuracy = (double)correct / (correct + incorrect);
            System.Console.WriteLine($"  Accuracy:    {accuracy:P1}");
        }
    }

    private async Task HandleAddAsync(string[] args)
    {
        if (args.Length == 0)
        {
            ConsoleUI.WriteError("Navedite tekst: add <text>");
            return;
        }

        var text = string.Join(" ", args);
        var message = await _queueService.AddMessageAsync(text);
        ConsoleUI.WriteSuccess($"Poruka dodana (ID: {message.Id}).");
    }

    private async Task HandleReviewAsync(string[] args)
    {
        var take = 10;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--take" && int.TryParse(args[i + 1], out var t))
            {
                take = t;
                break;
            }
        }

        var pending = await _reviewService.GetPendingReviewsAsync(take);
        
        if (pending.Count == 0)
        {
            ConsoleUI.WriteInfo("Nema poruka za review.");
            return;
        }

        ConsoleUI.WriteSubHeader($"Pending Review ({pending.Count} poruka)");

        foreach (var msg in pending)
        {
            System.Console.WriteLine();
            System.Console.WriteLine($"  ID: {msg.Id}");
            ConsoleUI.WriteMessagePreview(msg.Text, 70);
            System.Console.WriteLine();

            var lastPrediction = msg.Predictions.FirstOrDefault();
            if (lastPrediction != null)
            {
                System.Console.WriteLine($"  pSpam: {lastPrediction.PSpam:F3}");
            }

            System.Console.Write("  Labela (h=ham, s=spam, enter=skip): ");
            var input = System.Console.ReadLine()?.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(input)) continue;

            Label? label = input switch
            {
                "h" or "ham" => Label.Ham,
                "s" or "spam" => Label.Spam,
                _ => null
            };

            if (label == null)
            {
                ConsoleUI.WriteWarning("Preskočeno.");
                continue;
            }

            var (success, message) = await _reviewService.AddReviewAsync(msg.Id, label.Value);
            if (success)
            {
                ConsoleUI.WriteSuccess(message);

                // Provjeri auto-retrain
                var (shouldRetrain, currentGold, threshold) = await _reviewService.CheckAutoRetrainAsync();
                if (shouldRetrain)
                {
                    ConsoleUI.WriteInfo($"Auto-retrain triggered! ({currentGold}/{threshold})");
                    var model = await _trainingService.TrainModelAsync(TrainTemplate.Medium, activate: true);
                    ConsoleUI.WriteSuccess($"Novi model v{model.Version} treniran i aktiviran.");
                }
            }
            else
            {
                ConsoleUI.WriteError(message);
            }
        }
    }

    private async Task HandleSetAsync(string[] args)
    {
        if (args.Length < 2)
        {
            ConsoleUI.WriteError("Koristite: set thresholds <allow> <block> | set retrain-threshold <N>");
            return;
        }

        var settings = await _context.SystemSettings.FirstAsync();

        switch (args[0].ToLowerInvariant())
        {
            case "thresholds":
                if (args.Length < 3 || 
                    !double.TryParse(args[1], out var allow) ||
                    !double.TryParse(args[2], out var block))
                {
                    ConsoleUI.WriteError("Koristite: set thresholds <allow> <block>");
                    return;
                }
                settings.ThresholdAllow = allow;
                settings.ThresholdBlock = block;
                await _context.SaveChangesAsync();
                ConsoleUI.WriteSuccess($"Pragovi postavljeni: Allow={allow:F2}, Block={block:F2}");
                break;

            case "retrain-threshold":
                if (!int.TryParse(args[1], out var threshold))
                {
                    ConsoleUI.WriteError("Koristite: set retrain-threshold <N>");
                    return;
                }
                settings.RetrainGoldThreshold = threshold;
                await _context.SaveChangesAsync();
                ConsoleUI.WriteSuccess($"Retrain threshold postavljen na {threshold}");
                break;

            default:
                ConsoleUI.WriteError($"Nepoznata postavka: {args[0]}");
                break;
        }
    }

    private async Task HandleToggleAsync(string[] args)
    {
        if (args.Length < 2)
        {
            ConsoleUI.WriteError("Koristite: toggle auto-retrain <on|off>");
            return;
        }

        if (args[0].ToLowerInvariant() == "auto-retrain")
        {
            var settings = await _context.SystemSettings.FirstAsync();
            settings.AutoRetrainEnabled = args[1].ToLowerInvariant() == "on";
            await _context.SaveChangesAsync();
            ConsoleUI.WriteSuccess($"Auto-retrain {(settings.AutoRetrainEnabled ? "uključen" : "isključen")}");
        }
        else
        {
            ConsoleUI.WriteError($"Nepoznata opcija: {args[0]}");
        }
    }
}
