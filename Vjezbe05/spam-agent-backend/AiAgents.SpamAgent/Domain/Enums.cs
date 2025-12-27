/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SPAM AGENT - DOMAIN ENUMERACIJE
 * ═══════════════════════════════════════════════════════════════════════════════
 */

namespace AiAgents.SpamAgent.Domain;

/// <summary>
/// Labela poruke: Ham (legitimna) ili Spam
/// </summary>
public enum Label
{
    Ham = 0,
    Spam = 1
}

/// <summary>
/// Izvor poruke
/// </summary>
public enum MessageSource
{
    /// <summary>UCI dataset - importovane poruke</summary>
    Uci = 0,
    
    /// <summary>Runtime - poruke dodane tokom demo-a</summary>
    Runtime = 1
}

/// <summary>
/// Split za trening/validaciju
/// </summary>
public enum DataSplit
{
    /// <summary>Koristi se za trening</summary>
    TrainPool = 0,
    
    /// <summary>Koristi se za validaciju (fiksno)</summary>
    ValidationHoldout = 1
}

/// <summary>
/// Status poruke u sistemu
/// </summary>
public enum MessageStatus
{
    /// <summary>Samo u datasetu, nije u queue-u</summary>
    Dataset = 0,
    
    /// <summary>Čeka na procesiranje</summary>
    Queued = 1,
    
    /// <summary>Preuzeta od workera, procesiranje u toku (atomični claim)</summary>
    Processing = 2,
    
    /// <summary>Scorovana, ali još nije premještena</summary>
    Scored = 3,
    
    /// <summary>Klasificirana kao legitimna</summary>
    InInbox = 4,
    
    /// <summary>Klasificirana kao spam</summary>
    InSpam = 5,
    
    /// <summary>Nesigurna - čeka review</summary>
    PendingReview = 6
}

/// <summary>
/// Odluka agenta na osnovu pSpam vrijednosti
/// </summary>
public enum SpamDecision
{
    /// <summary>p &lt; ThresholdAllow → Inbox</summary>
    Allow = 0,
    
    /// <summary>ThresholdAllow &lt;= p &lt; ThresholdBlock → Review</summary>
    PendingReview = 1,
    
    /// <summary>p &gt;= ThresholdBlock → Spam</summary>
    Block = 2
}

/// <summary>
/// Template za trening (veličina training seta)
/// </summary>
public enum TrainTemplate
{
    /// <summary>Mali uzorak za brzi test (~500 poruka)</summary>
    Light = 0,
    
    /// <summary>Srednji uzorak (~2000 poruka)</summary>
    Medium = 1,
    
    /// <summary>Puni dataset</summary>
    Full = 2
}
