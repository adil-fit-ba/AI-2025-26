/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SOFTVERSKI INTELIGENTNI AGENTI - UNIFICIRANE APSTRAKCIJE
 * ═══════════════════════════════════════════════════════════════════════════════
 * 
 * Ovaj fajl definira UNIVERZALNE INTERFEJSE za sve tipove softverskih agenata.
 * 
 * KLJUČNA IDEJA: Svi agenti, bez obzira na tip, dijele istu arhitekturu:
 * 
 *      ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
 *      │  PERCEPCIJA │ ──► │   POLITIKA  │ ──► │   AKTUATOR  │
 *      │   (Sense)   │     │   (Think)   │     │    (Act)    │
 *      └─────────────┘     └─────────────┘     └─────────────┘
 *             │                   ▲                   │
 *             │                   │                   │
 *             │            ┌──────┴──────┐            │
 *             └──────────► │   UČENJE    │ ◄──────────┘
 *                          │  (Learn)    │
 *                          └─────────────┘
 * 
 * Razlika između tipova agenata je SAMO u implementaciji ovih komponenti!
 */

using System;

namespace AiAgents.Core;

// ════════════════════════════════════════════════════════════════════════════════
//                     1. UNIFICIRANE APSTRAKCIJE
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Agent mora "vidjeti" svijet oko sebe.
/// TPercept = tip podatka koji agent prima (temperatura, slika, tekst...)
/// </summary>
public interface IPerceptionSource<TPercept>
{
    TPercept? Observe();
    bool HasNext { get; }
}

/// <summary>
/// "Mozak" agenta - odlučuje šta uraditi na osnovu percepcije.
/// Može biti: IF-THEN pravila, ML model, Q-tabela, LLM...
/// </summary>
public interface IPolicy<TPercept, TAction>
{
    TAction SelectAction(TPercept percept);
}

/// <summary>
/// Izvršava akciju i vraća REZULTAT.
/// TResult može biti: (newState, reward) za RL, feedback od korisnika, itd.
/// </summary>
public interface IActuator<TAction, TResult>
{
    TResult Execute(TAction action);
}

/// <summary>
/// Komponenta za učenje - opciona, ne svi agenti uče.
/// TExperience = šta agent koristi za učenje.
/// </summary>
public interface ILearningComponent<TExperience>
{
    void Learn(TExperience experience);
}

/// <summary>
/// Osnovna jedinica - svaki agent mora moći napraviti jedan korak.
/// </summary>
public interface IAgent
{
    void Step();
    bool IsGoalReached { get; }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     2. GENERIČKI SOFTVERSKI AGENT
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// UNIVERZALNA IMPLEMENTACIJA AGENTA - svaki agent je kompozicija komponenti.
/// </summary>
public class SoftwareAgent<TPercept, TAction, TResult, TExperience> : IAgent
{
    protected readonly IPerceptionSource<TPercept> _perception;
    protected readonly IPolicy<TPercept, TAction> _policy;
    protected readonly IActuator<TAction, TResult> _actuator;
    protected readonly Func<TPercept, TAction, TResult, TExperience>? _experienceBuilder;
    protected readonly ILearningComponent<TExperience>? _learner;
    protected readonly Func<bool>? _goalChecker;

    public SoftwareAgent(
        IPerceptionSource<TPercept> perception,
        IPolicy<TPercept, TAction> policy,
        IActuator<TAction, TResult> actuator,
        Func<TPercept, TAction, TResult, TExperience>? experienceBuilder = null,
        ILearningComponent<TExperience>? learner = null,
        Func<bool>? goalChecker = null)
    {
        _perception = perception;
        _policy = policy;
        _actuator = actuator;
        _experienceBuilder = experienceBuilder;
        _learner = learner;
        _goalChecker = goalChecker;
    }

    public virtual bool IsGoalReached => _goalChecker?.Invoke() ?? false;

    /// <summary>
    /// JEDAN CIKLUS AGENTA: Sense → Think → Act → Learn
    /// </summary>
    public virtual void Step()
    {
        // 1. PERCEPCIJA
        var percept = _perception.Observe();
        if (percept == null) return;

        // 2. ODLUKA
        var action = _policy.SelectAction(percept);

        // 3. AKCIJA
        var result = _actuator.Execute(action);

        // 4. UČENJE (ako postoji)
        if (_learner != null && _experienceBuilder != null)
        {
            var experience = _experienceBuilder(percept, action, result);
            _learner.Learn(experience);
        }
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     3. POMOĆNE KLASE
// ════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Percepcija koja uvijek vraća ISTU vrijednost.
/// </summary>
public sealed class StaticPerception<T> : IPerceptionSource<T>
{
    private readonly T _value;
    public StaticPerception(T value) => _value = value;
    public T Observe() => _value;
    public bool HasNext => true;
}

/// <summary>
/// Percepcija koja vraća vrijednost iz FUNKCIJE.
/// Omogućava dinamičko dohvatanje iz queue-a, baze, itd.
/// </summary>
public sealed class DynamicPerception<T> : IPerceptionSource<T>
{
    private readonly Func<T?> _provider;
    private readonly Func<bool>? _hasNextChecker;
    
    public DynamicPerception(Func<T?> provider, Func<bool>? hasNextChecker = null)
    {
        _provider = provider;
        _hasNextChecker = hasNextChecker;
    }
    
    public T? Observe() => _provider();
    public bool HasNext => _hasNextChecker?.Invoke() ?? true;
}

/// <summary>
/// Aktuator koji samo ispisuje i vraća "prazan" rezultat.
/// </summary>
public sealed class ConsoleActuator<TAction> : IActuator<TAction, bool>
{
    private readonly string _name;
    public ConsoleActuator(string name) => _name = name;

    public bool Execute(TAction action)
    {
        Console.WriteLine($"  [{_name}] Akcija: {action}");
        return true;
    }
}
