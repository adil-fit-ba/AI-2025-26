/*
 * ═══════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - DI REGISTRATION
 * ═══════════════════════════════════════════════════════════════════════
 *
 * Extension metode za registraciju Spam Agent servisa u DI container.
 */

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AiAgents.SpamAgent.Infrastructure;
using AiAgents.SpamAgent.Application.Services;
using AiAgents.SpamAgent.Application.Queries;
using AiAgents.SpamAgent.Application.Agents;
using AiAgents.SpamAgent.ML;

namespace AiAgents.SpamAgent;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registruje sve Spam Agent servise u DI container.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureDb">Konfiguracija DbContext-a (npr. UseSqlite)</param>
    /// <param name="configureOptions">Opciona konfiguracija SpamAgentOptions</param>
    /// <returns>Service collection za chaining</returns>
    public static IServiceCollection AddSpamAgentServices(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDb,
        Action<SpamAgentOptions>? configureOptions = null)
    {
        // Konfiguracija
        var options = new SpamAgentOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        /*
         * EF Core DbContext
         * ─────────────────
         * DbContext je Scoped (po requestu / po scope-u).
         * Worker servisi rade "scope per iteration" tako da DbContext živi
         * samo tijekom jednog tick-a agenta.
         */
        services.AddDbContext<SpamAgentDbContext>(
            configureDb,
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Singleton);

        // ML Classifier (singleton za caching modela)
        services.AddSingleton<ISpamClassifier, MlNetSpamClassifier>();

        // Command/Use-case servisi (scoped)
        services.AddScoped<QueueService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<ScoringService>();

        // TrainingService treba modelsDirectory
        services.AddScoped<TrainingService>(sp =>
        {
            var context = sp.GetRequiredService<SpamAgentDbContext>();
            var classifier = sp.GetRequiredService<ISpamClassifier>();
            var opts = sp.GetRequiredService<SpamAgentOptions>();
            return new TrainingService(context, classifier, opts.ModelsDirectory);
        });

        // Query servisi (scoped)
        services.AddScoped<MessageQueryService>();
        services.AddScoped<AdminQueryService>();

        // Produkcijski agenti (scoped) - koriste ih BackgroundService-i
        services.AddScoped<ScoringAgent>();
        services.AddScoped<RetrainAgent>();

        // Database seeder (scoped)
        services.AddScoped<DatabaseSeeder>();

        return services;
    }

    /// <summary>
    /// Osigurava da je baza kreirana i vraća putanju do dataset fajla.
    /// </summary>
    public static async Task<string> InitializeSpamAgentAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SpamAgentDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<SpamAgentOptions>();

        // Kreiraj bazu ako ne postoji
        await context.Database.EnsureCreatedAsync();

        // Osiguraj da folder za modele postoji (da ne puca pri prvom snimanju)
        if (!string.IsNullOrWhiteSpace(options.ModelsDirectory))
        {
            System.IO.Directory.CreateDirectory(options.ModelsDirectory);
        }

        // Pokušaj učitati aktivni model
        var classifier = scope.ServiceProvider.GetRequiredService<ISpamClassifier>();
        var settings = await context.SystemSettings
            .Include(s => s.ActiveModelVersion)
            .FirstOrDefaultAsync();

        if (settings?.ActiveModelVersion != null)
        {
            var modelPath = settings.ActiveModelVersion.ModelFilePath;
            if (System.IO.File.Exists(modelPath))
            {
                try
                {
                    await classifier.LoadModelAsync(modelPath);
                }
                catch
                {
                    // Ignoriši greške pri učitavanju - model će se učitati kasnije
                }
            }
        }

        return options.DatasetPath;
    }
}
