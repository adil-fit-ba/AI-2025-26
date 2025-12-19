/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          SOFTVERSKI INTELIGENTNI AGENTI - UNIFICIRANE APSTRAKCIJE
 * ═══════════════════════════════════════════════════════════════════════════════
 * 
 * Ovaj fajl definira UNIVERZALNE INTERFEJSE za sve tipove softverskih agenata
 * i demonstrira ih kroz 5 različitih primjera.
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

/*
 * PERCEPCIJA (Sense)
 * ──────────────────
 * Agent mora "vidjeti" svijet oko sebe.
 * 
 * TPercept = tip podatka koji agent prima (temperatura, slika, tekst...)
 */
public interface IPerceptionSource<TPercept>
{
    TPercept Observe();
}

/*
 * POLITIKA (Think / Decide)
 * ─────────────────────────
 * "Mozak" agenta - odlučuje šta uraditi na osnovu percepcije.
 * 
 * Može biti: IF-THEN pravila, ML model, Q-tabela, LLM...
 */
public interface IPolicy<TPercept, TAction>
{
    TAction SelectAction(TPercept percept);
}

/*
 * AKTUATOR (Act)
 * ──────────────
 * Izvršava akciju i vraća REZULTAT.
 * 
 * TResult može biti:
 * - (newState, reward) za RL agente
 * - jednostavan void wrapper za rule-based
 * - feedback od korisnika za human-in-loop
 */
public interface IActuator<TAction, TResult>
{
    TResult Execute(TAction action);
}

/*
 * UČENJE (Learn) - OPCIONO
 * ────────────────────────
 * Ne svi agenti uče! Rule-based agenti nemaju ovu komponentu.
 * 
 * TExperience = šta agent koristi za učenje:
 * - (state, action, reward, nextState) za RL
 * - (predicted, actual) za Supervised Learning
 * - (action, userRating) za Human-in-loop
 */
public interface ILearningComponent<TExperience>
{
    void Learn(TExperience experience);
}

/*
 * AGENT - osnovna jedinica
 * ────────────────────────
 * Svaki agent mora moći napraviti jedan korak (Sense-Think-Act-Learn ciklus).
 * IsGoalReached je opciono - neki agenti rade beskonačno (chatbot),
 * neki imaju jasan cilj (usisivač očisti sve).
 */
public interface IAgent
{
    void Step();
    bool IsGoalReached { get; }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     2. GENERIČKI SOFTVERSKI AGENT
// ════════════════════════════════════════════════════════════════════════════════

/*
 * UNIVERZALNA IMPLEMENTACIJA AGENTA
 * ─────────────────────────────────
 * Ova klasa pokazuje da je SVAKI agent samo kompozicija komponenti.
 * 
 * TPercept = šta agent vidi
 * TAction = šta agent može uraditi
 * TResult = šta dobije nazad od okoline
 * TExperience = šta koristi za učenje
 */
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

    /*
     * JEDAN CIKLUS AGENTA
     * ───────────────────
     *   1. SENSE  → Prikupi informacije iz okoline
     *   2. THINK  → Odluči šta uraditi
     *   3. ACT    → Izvrši akciju, dobij rezultat
     *   4. LEARN  → Poboljšaj se na osnovu iskustva (opciono)
     */
    public virtual void Step()
    {
        // 1. PERCEPCIJA
        var percept = _perception.Observe();

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

/*
 * STATIC PERCEPTION
 * ─────────────────
 * Percepcija koja uvijek vraća ISTU vrijednost.
 * Korisno za testiranje ili kada je percepcija fiksna.
 */
public sealed class StaticPerception<T> : IPerceptionSource<T>
{
    private readonly T _value;
    public StaticPerception(T value) => _value = value;
    public T Observe() => _value;
}

/*
 * DYNAMIC PERCEPTION
 * ──────────────────
 * Percepcija koja vraća vrijednost iz FUNKCIJE.
 * 
 * ZAŠTO JE OVO VAŽNO?
 * ───────────────────
 * U realnom svijetu, percepcija se MIJENJA:
 *   • Email inbox - novi emailovi dolaze kontinuirano
 *   • Senzor temperature - vrijednost fluktuira
 *   • Ticket queue - novi tiketi stižu
 *   • Kamera - slika se mijenja
 * 
 * DynamicPerception omogućava agentu da SVAKI PUT kad pozove Observe()
 * dobije NOVU, AKTUELNU vrijednost iz okoline.
 * 
 * PRIMJER KORIŠTENJA:
 * ───────────────────
 *   var queue = new Queue<Email>();
 *   var perception = new DynamicPerception<Email>(() => queue.Dequeue());
 *   
 *   // Svaki poziv Observe() vraća SLJEDEĆI email iz queue-a!
 *
 * RAZLIKA OD StaticPerception:
 * ────────────────────────────
 *   StaticPerception  → ista vrijednost svaki put
 *   DynamicPerception → nova vrijednost svaki put (iz funkcije)
 */
public sealed class DynamicPerception<T> : IPerceptionSource<T>
{
    private readonly Func<T> _provider;
    public DynamicPerception(Func<T> provider) => _provider = provider;
    public T Observe() => _provider();
}

// Aktuator koji samo ispisuje i vraća "prazan" rezultat
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
