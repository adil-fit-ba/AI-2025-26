/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - EF CORE DB CONTEXT
 * ═══════════════════════════════════════════════════════════════════════════════
 */

using Microsoft.EntityFrameworkCore;
using AiAgents.SpamAgent.Domain;

namespace AiAgents.SpamAgent.Infrastructure;

public class SpamAgentDbContext : DbContext
{
    public SpamAgentDbContext(DbContextOptions<SpamAgentDbContext> options) 
        : base(options)
    {
    }

    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Message indeksi
        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.Source, m.Split });
        
        modelBuilder.Entity<Message>()
            .HasIndex(m => m.Status);
        
        modelBuilder.Entity<Message>()
            .HasIndex(m => m.CreatedAtUtc);

        // Review - 1 po poruci
        modelBuilder.Entity<Review>()
            .HasIndex(r => r.MessageId)
            .IsUnique();

        // ModelVersion - unique Version
        modelBuilder.Entity<ModelVersion>()
            .HasIndex(mv => mv.Version)
            .IsUnique();

        // SystemSettings - singleton seed
        modelBuilder.Entity<SystemSettings>()
            .HasData(new SystemSettings
            {
                Id = 1,
                ThresholdAllow = 0.30,
                ThresholdBlock = 0.70,
                RetrainGoldThreshold = 100,
                NewGoldSinceLastTrain = 0,
                AutoRetrainEnabled = true
            });

        // Konverzija enum-a u string za čitljivost u bazi
        modelBuilder.Entity<Message>()
            .Property(m => m.Source)
            .HasConversion<string>();
        
        modelBuilder.Entity<Message>()
            .Property(m => m.Split)
            .HasConversion<string>();
        
        modelBuilder.Entity<Message>()
            .Property(m => m.TrueLabel)
            .HasConversion<string>();
        
        modelBuilder.Entity<Message>()
            .Property(m => m.Status)
            .HasConversion<string>();

        modelBuilder.Entity<Prediction>()
            .Property(p => p.Decision)
            .HasConversion<string>();

        modelBuilder.Entity<Review>()
            .Property(r => r.Label)
            .HasConversion<string>();

        modelBuilder.Entity<ModelVersion>()
            .Property(mv => mv.TrainTemplate)
            .HasConversion<string>();
    }
}
