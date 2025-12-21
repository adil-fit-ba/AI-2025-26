/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - CONFIGURATION OPTIONS
 * ═══════════════════════════════════════════════════════════════════════════════
 */

namespace AiAgents.SpamAgent;

/// <summary>
/// Opcije konfiguracije za Spam Agent servise.
/// </summary>
public class SpamAgentOptions
{
    /// <summary>
    /// Direktorij za čuvanje ML modela.
    /// </summary>
    public string ModelsDirectory { get; set; } = "models";

    /// <summary>
    /// Putanja do dataset fajla (za import).
    /// </summary>
    public string DatasetPath { get; set; } = "Dataset/SMSSpamCollection";

    /// <summary>
    /// Default prag ispod kojeg je ALLOW (inbox).
    /// </summary>
    public double DefaultThresholdAllow { get; set; } = 0.30;

    /// <summary>
    /// Default prag iznad kojeg je BLOCK (spam).
    /// </summary>
    public double DefaultThresholdBlock { get; set; } = 0.70;

    /// <summary>
    /// Default broj gold labela za auto-retrain.
    /// </summary>
    public int DefaultRetrainGoldThreshold { get; set; } = 100;
}
