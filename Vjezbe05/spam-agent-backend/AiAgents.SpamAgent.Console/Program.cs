/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - CONSOLE APPLICATION
 * ═══════════════════════════════════════════════════════════════════════════════
 * 
 * Demonstracija softverskog inteligentnog agenta za klasifikaciju SMS spam-a.
 * 
 * Arhitektura agenta:
 *   Sense  → Uzmi poruku iz queue-a
 *   Think  → ML.NET model izračuna pSpam
 *   Act    → Odluka (Allow/Pending/Block) + update statusa
 *   Learn  → Moderator review + auto-retrain
 * 
 * Za pomoć unesite: help
 */

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using AiAgents.SpamAgent.Infrastructure;
using AiAgents.SpamAgent.Application.Services;
using AiAgents.SpamAgent.ML;
using AiAgents.SpamAgent.Console.CLI;

namespace AiAgents.SpamAgent.Console;

class Program
{
    static async Task Main(string[] args)
    {
        System.Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Učitaj konfiguraciju
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        // Putanje
        var baseDir = AppContext.BaseDirectory;
        var dataDir = Path.Combine(baseDir, "data");
        var modelsDir = Path.Combine(baseDir, "models");
        var datasetDir = Path.Combine(baseDir, "Dataset");

        // Kreiraj direktorije ako ne postoje
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(modelsDir);

        var dbPath = Path.Combine(dataDir, "spam_agent.db");
        var datasetPath = Path.Combine(datasetDir, "SMSSpamCollection");

        // Provjeri da li dataset postoji
        if (!File.Exists(datasetPath))
        {
            ConsoleUI.WriteError($"Dataset nije pronađen: {datasetPath}");
            ConsoleUI.WriteInfo("Kopirajte SMSSpamCollection fajl u Dataset folder.");
            return;
        }

        // Setup EF Core
        var optionsBuilder = new DbContextOptionsBuilder<SpamAgentDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        await using var context = new SpamAgentDbContext(optionsBuilder.Options);
        
        // Kreiraj/ažuriraj bazu
        await context.Database.EnsureCreatedAsync();

        // Kreiraj servise
        var classifier = new MlNetSpamClassifier();
        var seeder = new DatabaseSeeder(context);
        var queueService = new QueueService(context);
        var reviewService = new ReviewService(context);
        var trainingService = new TrainingService(context, classifier, modelsDir);
        var scoringService = new ScoringService(context, classifier);

        // Pokušaj učitati aktivni model
        var modelLoaded = await trainingService.LoadActiveModelAsync();

        // Command handler
        var handler = new CommandHandler(
            context,
            seeder,
            queueService,
            reviewService,
            trainingService,
            scoringService,
            classifier,
            datasetPath
        );

        // Welcome
        ConsoleUI.WriteHeader("SPAM AGENT - INTELIGENTNI SOFTVERSKI AGENT");
        System.Console.WriteLine();
        System.Console.WriteLine("  SMS Spam klasifikacija sa ML.NET");
        System.Console.WriteLine("  Arhitektura: Sense → Think → Act → Learn");
        System.Console.WriteLine();
        
        if (!modelLoaded)
        {
            ConsoleUI.WriteWarning("Nema aktivnog modela. Pokrenite sljedeće komande:");
            System.Console.WriteLine("    1. import          (učitaj UCI dataset)");
            System.Console.WriteLine("    2. train medium --activate  (treniraj model)");
            System.Console.WriteLine("    3. enqueue 20      (dodaj poruke u queue)");
            System.Console.WriteLine("    4. run 20          (procesiraj poruke)");
        }
        else
        {
            ConsoleUI.WriteSuccess("Aktivni model učitan. Unesite 'status' za pregled.");
        }

        System.Console.WriteLine();
        System.Console.WriteLine("  Unesite 'help' za listu komandi.");
        System.Console.WriteLine();

        // Main loop
        while (true)
        {
            System.Console.ForegroundColor = ConsoleColor.Cyan;
            System.Console.Write("spam-agent> ");
            System.Console.ResetColor();

            var input = System.Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(input)) continue;
            
            if (input.Trim().ToLowerInvariant() == "exit")
            {
                ConsoleUI.WriteInfo("Doviđenja!");
                break;
            }

            await handler.HandleAsync(input);
        }
    }
}
