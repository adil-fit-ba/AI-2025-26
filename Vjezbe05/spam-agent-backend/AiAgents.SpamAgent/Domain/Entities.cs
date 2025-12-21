/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - DOMAIN ENTITETI
 * ═══════════════════════════════════════════════════════════════════════════════
 */

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AiAgents.SpamAgent.Domain;

/// <summary>
/// Poruka - SMS tekst sa metapodacima
/// </summary>
public class Message
{
    [Key]
    public long Id { get; set; }
    
    /// <summary>Izvor: UCI dataset ili Runtime</summary>
    public MessageSource Source { get; set; }
    
    /// <summary>Tekst poruke</summary>
    [Required]
    public string Text { get; set; } = string.Empty;
    
    /// <summary>Vrijeme kreiranja</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    /// <summary>Split za trening/validaciju (null za runtime bez split-a)</summary>
    public DataSplit? Split { get; set; }
    
    /// <summary>Prava labela (ako je poznata)</summary>
    public Label? TrueLabel { get; set; }
    
    /// <summary>Trenutni status u sistemu</summary>
    public MessageStatus Status { get; set; } = MessageStatus.Dataset;
    
    /// <summary>Zadnja verzija modela koja je scorovala ovu poruku</summary>
    public int? LastModelVersionId { get; set; }
    
    [ForeignKey(nameof(LastModelVersionId))]
    public ModelVersion? LastModelVersion { get; set; }
    
    // Navigacijska svojstva
    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
    public Review? Review { get; set; }
}

/// <summary>
/// Predikcija - rezultat scorovanja poruke
/// </summary>
public class Prediction
{
    [Key]
    public long Id { get; set; }
    
    /// <summary>FK na poruku</summary>
    public long MessageId { get; set; }
    
    [ForeignKey(nameof(MessageId))]
    public Message Message { get; set; } = null!;
    
    /// <summary>FK na verziju modela</summary>
    public int ModelVersionId { get; set; }
    
    [ForeignKey(nameof(ModelVersionId))]
    public ModelVersion ModelVersion { get; set; } = null!;
    
    /// <summary>Vjerovatnoća spam-a (0.0 - 1.0)</summary>
    public double PSpam { get; set; }
    
    /// <summary>Odluka na osnovu pragova</summary>
    public SpamDecision Decision { get; set; }
    
    /// <summary>Vrijeme predikcije</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Review - moderatorova odluka (gold label)
/// </summary>
public class Review
{
    [Key]
    public long Id { get; set; }
    
    /// <summary>FK na poruku (1 review po poruci)</summary>
    public long MessageId { get; set; }
    
    [ForeignKey(nameof(MessageId))]
    public Message Message { get; set; } = null!;
    
    /// <summary>Labela koju je moderator dodijelio</summary>
    public Label Label { get; set; }
    
    /// <summary>Ko je napravio review</summary>
    [MaxLength(100)]
    public string ReviewedBy { get; set; } = "console-admin";
    
    /// <summary>Vrijeme review-a</summary>
    public DateTime ReviewedAtUtc { get; set; } = DateTime.UtcNow;
    
    /// <summary>Opcionalna napomena</summary>
    public string? Note { get; set; }
}

/// <summary>
/// Verzija ML modela
/// </summary>
public class ModelVersion
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>Verzija (1, 2, 3...)</summary>
    public int Version { get; set; }
    
    /// <summary>Tip trenera (npr. "SDCA Logistic Regression")</summary>
    [MaxLength(200)]
    public string TrainerType { get; set; } = "SDCA Logistic Regression";
    
    /// <summary>Featurizer (npr. "FeaturizeText TF-IDF")</summary>
    [MaxLength(200)]
    public string Featurizer { get; set; } = "FeaturizeText TF-IDF";
    
    /// <summary>Template korišten za trening</summary>
    public TrainTemplate TrainTemplate { get; set; }
    
    /// <summary>Broj primjera u training setu</summary>
    public int TrainSetSize { get; set; }
    
    /// <summary>Broj gold labela uključenih</summary>
    public int GoldIncludedCount { get; set; }
    
    /// <summary>Broj primjera u validation setu</summary>
    public int ValidationSetSize { get; set; }
    
    // Metrike
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1 { get; set; }
    
    /// <summary>Pragovi korišteni pri treningu</summary>
    public double ThresholdAllow { get; set; }
    public double ThresholdBlock { get; set; }
    
    /// <summary>Putanja do model fajla</summary>
    [MaxLength(500)]
    public string ModelFilePath { get; set; } = string.Empty;
    
    /// <summary>Vrijeme kreiranja</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    /// <summary>Da li je ovo aktivni model</summary>
    public bool IsActive { get; set; }
    
    // Navigacijska svojstva
    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
}

/// <summary>
/// Sistemske postavke (singleton - jedan red)
/// </summary>
public class SystemSettings
{
    [Key]
    public int Id { get; set; } = 1;
    
    /// <summary>FK na aktivnu verziju modela</summary>
    public int? ActiveModelVersionId { get; set; }
    
    [ForeignKey(nameof(ActiveModelVersionId))]
    public ModelVersion? ActiveModelVersion { get; set; }
    
    /// <summary>Prag ispod kojeg je ALLOW (inbox)</summary>
    public double ThresholdAllow { get; set; } = 0.30;
    
    /// <summary>Prag iznad kojeg je BLOCK (spam)</summary>
    public double ThresholdBlock { get; set; } = 0.70;
    
    /// <summary>Broj gold labela nakon kojeg se triggeruje retrain</summary>
    public int RetrainGoldThreshold { get; set; } = 100;
    
    /// <summary>Broj novih gold labela od zadnjeg treninga</summary>
    public int NewGoldSinceLastTrain { get; set; }
    
    /// <summary>Da li je auto-retrain uključen</summary>
    public bool AutoRetrainEnabled { get; set; } = true;
    
    /// <summary>Vrijeme zadnjeg treninga</summary>
    public DateTime? LastRetrainAtUtc { get; set; }
}
