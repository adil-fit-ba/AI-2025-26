/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT WEB - MAIN ENTRY POINT
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 * Web API + SignalR za SMS Spam klasifikaciju.
 *
 * Arhitektura agenta:
 *   - ScoringAgentRunner (Background): Sense → Think → Act
 *   - RetrainAgentRunner (Background): Sense → Think → Act → Learn
 *   - Simulator (Background, opciono): generira poruke za demo
 *
 * Svi servisi su registrovani kroz AddSpamAgentServices() extension metodu.
 */

using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using AiAgents.SpamAgent;
using AiAgents.SpamAgent.ML;
using AiAgents.SpamAgent.Web.Hubs;
using AiAgents.SpamAgent.Web.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════════════
//                     CONFIGURATION
// ═══════════════════════════════════════════════════════════════════════════════

var baseDir = AppContext.BaseDirectory;
var dataDir = Path.Combine(baseDir, "data");
var modelsDir = Path.Combine(baseDir, "models");
var datasetPath = Path.Combine(baseDir, "Dataset", "SMSSpamCollection");

Directory.CreateDirectory(dataDir);
Directory.CreateDirectory(modelsDir);

var connectionString = $"Data Source={Path.Combine(dataDir, "spam_agent.db")}";

// ═══════════════════════════════════════════════════════════════════════════════
//                     SPAM AGENT SERVICES (from shared library)
// ═══════════════════════════════════════════════════════════════════════════════

builder.Services.AddSpamAgentServices(
    db => db.UseSqlite(connectionString),
    options =>
    {
        options.ModelsDirectory = modelsDir;
        options.DatasetPath = datasetPath;
    }
);

// ═══════════════════════════════════════════════════════════════════════════════
//                     WEB SERVICES
// ═══════════════════════════════════════════════════════════════════════════════

// SignalR
builder.Services.AddSignalR();

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Spam Agent API",
        Version = "v1",
        Description = @"
SMS Spam Classification Agent API

**Arhitektura:**
- Sense → Think → Act → Learn ciklus
- ML.NET binary classification (SDCA Logistic Regression)
- 3-zone odlučivanje: Allow / PendingReview / Block

**Real-time:** SignalR hub na `/hubs/spamAgent`
",
        Contact = new OpenApiContact
        {
            Name = "AI Agents Demo"
        }
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("OpenCors", policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true) // dozvoli SVE origin-e
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials(); // VAŽNO za SignalR
    });
});

// Background Services
builder.Services.AddHostedService<ScoringWorkerService>();
builder.Services.AddHostedService<RetrainWorkerService>();
builder.Services.AddSingleton<SimulatorService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SimulatorService>());

// ═══════════════════════════════════════════════════════════════════════════════
//                     BUILD APP
// ═══════════════════════════════════════════════════════════════════════════════

var app = builder.Build();

// Inicijalizacija baze i učitavanje modela
await app.Services.InitializeSpamAgentAsync();

// Swagger (development)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Spam Agent API v1");
        c.RoutePrefix = string.Empty; // Swagger na root-u
    });
}

app.UseRouting();

// CORS
app.UseCors("OpenCors");


// Endpoints
app.MapControllers();
app.MapHub<SpamAgentHub>("/hubs/spamAgent").RequireCors("OpenCors");

// Welcome message
Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine("          SPAM AGENT WEB API");
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine("  Swagger UI:    http://localhost:5000");
Console.WriteLine("  SignalR Hub:   http://localhost:5000/hubs/spamAgent");
Console.WriteLine();
Console.WriteLine("  Quick Start:");
Console.WriteLine("    1. POST /api/admin/import     → importuj dataset");
Console.WriteLine("    2. POST /api/admin/train      → treniraj model");
Console.WriteLine("    3. POST /api/messages/enqueue → dodaj poruke u queue");
Console.WriteLine("    4. GET  /api/messages/recent  → gledaj rezultate");
Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine();

app.Run("http://localhost:5000");
