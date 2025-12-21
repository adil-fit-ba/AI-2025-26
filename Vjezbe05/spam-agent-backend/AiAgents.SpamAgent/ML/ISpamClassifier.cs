/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - ML INTERFACE
 * ═══════════════════════════════════════════════════════════════════════════════
 */

using System.Collections.Generic;
using System.Threading.Tasks;

namespace AiAgents.SpamAgent.ML;

/// <summary>
/// Interface za ML klasifikator - omogućava zamjenu implementacije.
/// </summary>
public interface ISpamClassifier
{
    /// <summary>
    /// Trenira model na zadanim podacima.
    /// </summary>
    /// <param name="trainingData">Lista (text, isSpam) parova</param>
    /// <param name="modelPath">Putanja gdje će se sačuvati model</param>
    /// <returns>Putanja do sačuvanog modela</returns>
    Task<string> TrainAsync(IEnumerable<TrainingSample> trainingData, string modelPath);

    /// <summary>
    /// Evaluira model na zadanim podacima.
    /// </summary>
    /// <param name="validationData">Lista (text, isSpam) parova</param>
    /// <returns>Metrike evaluacije</returns>
    Task<EvaluationMetrics> EvaluateAsync(IEnumerable<TrainingSample> validationData);

    /// <summary>
    /// Učitava model sa diska.
    /// </summary>
    /// <param name="modelPath">Putanja do model fajla</param>
    Task LoadModelAsync(string modelPath);

    /// <summary>
    /// Predviđa vjerovatnoću spam-a za tekst.
    /// </summary>
    /// <param name="text">Tekst poruke</param>
    /// <returns>Vjerovatnoća da je spam (0.0 - 1.0)</returns>
    Task<double> PredictAsync(string text);

    /// <summary>
    /// Predviđa za batch tekstova.
    /// </summary>
    Task<IList<double>> PredictBatchAsync(IEnumerable<string> texts);

    /// <summary>
    /// Da li je model učitan
    /// </summary>
    bool IsModelLoaded { get; }
}

/// <summary>
/// Uzorak za trening
/// </summary>
public class TrainingSample
{
    public string Text { get; set; } = string.Empty;
    public bool IsSpam { get; set; }

    public TrainingSample() { }
    
    public TrainingSample(string text, bool isSpam)
    {
        Text = text;
        IsSpam = isSpam;
    }
}

/// <summary>
/// Metrike evaluacije modela
/// </summary>
public class EvaluationMetrics
{
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
    public double AreaUnderRocCurve { get; set; }
    public int TruePositives { get; set; }
    public int TrueNegatives { get; set; }
    public int FalsePositives { get; set; }
    public int FalseNegatives { get; set; }

    public override string ToString()
    {
        return $"Accuracy: {Accuracy:P2}, Precision: {Precision:P2}, Recall: {Recall:P2}, F1: {F1Score:P2}";
    }
}
