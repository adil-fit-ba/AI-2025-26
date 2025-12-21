/*
 * ════════════════════════════════════════════════════════════════════════════════
 *                     PRIMJER C: REINFORCEMENT LEARNING AGENT (Robot)
 * ════════════════════════════════════════════════════════════════════════════════
 */

using System;
using AiAgents.Core;

namespace AiAgents.Demos.ConsoleApp;

/*
 * ┌─────────────────────────────────────────────────────────────────────────────┐
 * │  REINFORCEMENT LEARNING AGENT                                              │
 * ├─────────────────────────────────────────────────────────────────────────────┤
 * │  • Politika = Q-tabela (uči se iz nagrada)                                 │
 * │  • Experience = (state, action, reward, nextState)                         │
 * │  • Ima CILJ - završava kad stigne do cilja                                 │
 * │                                                                             │
 * │  PRIMJER: Robot na 1D traci, cilj je pozicija 5                            │
 * │                                                                             │
 * │    [0] [1] [2] [3] [4] [5]                                                  │
 * │     ●──────────────────►★                                                  │
 * └─────────────────────────────────────────────────────────────────────────────┘
 */

public readonly record struct RobotState(int Position);

public enum RobotAction { Left, Right }

// Rezultat akcije u RL: novo stanje + nagrada
public readonly record struct RLStepResult(RobotState NextState, double Reward);

// Experience za RL: SARSA tuple
public readonly record struct RLExperience(
    RobotState State,
    RobotAction Action,
    double Reward,
    RobotState NextState
);

// Okolina: 1D traka sa ciljem
public sealed class RobotEnvironment : IPerceptionSource<RobotState>, IActuator<RobotAction, RLStepResult>
{
    private int _position = 0;
    private readonly int _goal = 5;

    // Ima "sljedećeg" sve dok epizoda nije završena.
    // Okolina je uvijek "observable"; za kraj epizode koristiš goalChecker/maxSteps
    public bool HasNext => true;

    public RobotState Observe() => new RobotState(_position);

    public RLStepResult Execute(RobotAction action)
    {
        // Pomjeri robota
        if (action == RobotAction.Right && _position < 5)
            _position++;
        else if (action == RobotAction.Left && _position > 0)
            _position--;

        // Izračunaj nagradu
        double reward = _position == _goal ? +10 : -1;
        return new RLStepResult(new RobotState(_position), reward);
    }

    public bool IsAtGoal => _position == _goal;
    public void Reset() => _position = 0;
}

// Q-Learning politika
public sealed class SimpleQPolicy : IPolicy<RobotState, RobotAction>
{
    private readonly double[,] _q = new double[6, 2];  // 6 pozicija, 2 akcije
    private readonly Random _rng = new();
    public double Epsilon { get; set; } = 0.3;

    public RobotAction SelectAction(RobotState state)
    {
        if (_rng.NextDouble() < Epsilon)
            return _rng.NextDouble() > 0.5 ? RobotAction.Right : RobotAction.Left;

        int pos = state.Position;
        return _q[pos, 0] > _q[pos, 1] ? RobotAction.Left : RobotAction.Right;
    }

    public double GetQ(int pos, int action) => _q[pos, action];
    public void SetQ(int pos, int action, double value) => _q[pos, action] = value;
}

// Q-Learning updater
public sealed class QLearner : ILearningComponent<RLExperience>
{
    private readonly SimpleQPolicy _policy;
    private readonly double _alpha = 0.1, _gamma = 0.95;

    public QLearner(SimpleQPolicy policy) => _policy = policy;

    public void Learn(RLExperience exp)
    {
        int s = exp.State.Position;
        int a = (int)exp.Action;
        int ns = exp.NextState.Position;

        double currentQ = _policy.GetQ(s, a);
        double maxNextQ = Math.Max(_policy.GetQ(ns, 0), _policy.GetQ(ns, 1));
        double newQ = currentQ + _alpha * (exp.Reward + _gamma * maxNextQ - currentQ);

        _policy.SetQ(s, a, newQ);

        Console.WriteLine($"  [RLAgent] Pos: {s} → {ns}, Action: {exp.Action}, " +
                          $"Reward: {exp.Reward:+0;-0}, Q: {currentQ:F2} → {newQ:F2}");
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     DEMO METODA
// ════════════════════════════════════════════════════════════════════════════════

public static class RobotDemo
{
    public static void Run()
    {
        /*
        * ───────────────────────────────────────────────────────────────────────────────
        *  DEMO: REINFORCEMENT LEARNING INTELIGENTNI AGENT
        *        (Robot na traci – klasični RL / Vacuum Cleaner analogija)
        * ───────────────────────────────────────────────────────────────────────────────
        *
        *  POENTA DEMO-a
        *  ─────────────
        *  Ovaj primjer demonstrira klasičnog REINFORCEMENT LEARNING (RL) AGENTA,
        *  kakav se u AI literaturi najčešće uvodi kroz:
        *    • Vacuum Cleaner agenta
        *    • Grid World
        *    • jednostavnu navigaciju robota
        *
        *  Ovdje koristimo namjerno JEDNOSTAVNU okolinu da bi se fokus stavio
        *  na agentičku arhitekturu, a ne na kompleksnost okruženja.
        *
        *  OKOLINA (Environment)
        *  ────────────────────
        *  Okolina je linearna traka sa pozicijama [0..5]:
        *
        *      [0] [1] [2] [3] [4] [5★]
        *       ●──────────────────►  cilj
        *
        *  Agent počinje na poziciji 0 i cilj mu je da stigne do pozicije 5.
        *
        *  Za svaki potez:
        *    • dobija malu negativnu nagradu (-1)
        *    • dobija veliku pozitivnu nagradu (+10) kada stigne do cilja
        *
        *  Ovo forsira agenta da:
        *    • pronađe NAJKRAĆI put
        *    • ne luta bespotrebno
        *
        *  POVEZNICA SA VACUUM CLEANER AGENTOM
        *  ──────────────────────────────────
        *  Vacuum Cleaner (klasični primjer):
        *    • State   = (lokacija, prljavo/čisto)
        *    • Actions = {Left, Right, Suck}
        *    • Reward  = pozitivna nagrada za čišćenje
        *
        *  Ovdje:
        *    • State   = RobotState (pozicija)
        *    • Actions = {Left, Right}
        *    • Reward  = +10 / -1
        *
        *  Struktura je IDENTIČNA, samo je okolina pojednostavljena.
        *
        *  GENERIC PARAMETRI AGENTA
        *  ───────────────────────
        *  TPercept    = RobotState
        *      → šta agent vidi: trenutnu poziciju
        *
        *  TAction     = RobotAction
        *      → šta agent može uraditi: pomjeriti se lijevo ili desno
        *
        *  TResult     = RLStepResult
        *      → odgovor okoline: (novo stanje, nagrada)
        *
        *  TExperience = RLExperience
        *      → iskustvo za učenje:
        *        (state, action, reward, nextState)
        *
        *  KOMPONENTE AGENTA
        *  ─────────────────
        *
        *  1) PERCEPCIJA (Sense)
        *     - RobotEnvironment.Observe()
        *     Agent vidi trenutno stanje okoline (poziciju robota).
        *
        *  2) POLITIKA (Think)
        *     - SimpleQPolicy
        *     Politika koristi Q-tabelu i epsilon-greedy strategiju:
        *       • sa vjerovatnoćom epsilon → istraživanje (random akcija)
        *       • inače → iskorištavanje (najbolja poznata akcija)
        *
        *     Politika u početku NE ZNA ništa.
        *     Znanje se gradi isključivo kroz iskustvo.
        *
        *  3) AKCIJA (Act)
        *     - RobotEnvironment.Execute(action)
        *     Agent izvršava akciju, okolina:
        *       • mijenja stanje
        *       • dodjeljuje nagradu
        *
        *  4) UČENJE (Learn)
        *     - QLearner
        *     Implementira Q-learning pravilo:
        *
        *       Q(s,a) ← Q(s,a) + α [ r + γ max Q(s',a') − Q(s,a) ]
        *
        *     Gdje:
        *       • α (alpha)  = stopa učenja
        *       • γ (gamma)  = faktor diskontiranja
        *
        *     Nakon svakog koraka, agent postaje malo "pametniji".
        *
        *  AGENTIČKI CIKLUS (Sense → Think → Act → Learn)
        *  ─────────────────────────────────────────────
        *  Svaki poziv agent.Step() radi:
        *
        *    1) agent opaža stanje (pozicija)
        *    2) bira akciju (Left / Right)
        *    3) izvršava akciju u okolini
        *    4) dobija nagradu
        *    5) ažurira Q-vrijednosti
        *
        *  Ovaj ciklus se ponavlja dok:
        *    • cilj nije postignut
        *    • ili dok ne istekne maksimalan broj koraka
        *
        *  CILJ (Goal-oriented agent)
        *  ─────────────────────────
        *  Agent ima eksplicitan cilj:
        *    • stići do pozicije 5
        *
        *  goalChecker omogućava agentu da:
        *    • prepozna kada je zadatak završen
        *    • završi epizodu
        *
        *  ZAŠTO JE OVO INTELIGENTNI AGENT?
        *  ───────────────────────────────
        *    ✔ ima percepciju okoline
        *    ✔ donosi odluke
        *    ✔ utiče na okolinu
        *    ✔ uči iz nagrade
        *    ✔ ponašanje se poboljšava kroz vrijeme
        *
        *  Ovo je referentni primjer RL agenta u AI literaturi,
        *  implementiran u čisto softverskom obliku.
        *
        *  KLJUČNA REČENICA ZA STUDENTE
        *  ───────────────────────────
        *  "Ovo je isti tip agenta kao Vacuum Cleaner –
        *   samo sa Q-tabelom umjesto pravila."
        *
        * ───────────────────────────────────────────────────────────────────────────────
        */

        Console.WriteLine("\n┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│  3. REINFORCEMENT LEARNING: Robot na traci                  │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
        Console.WriteLine("  Cilj: [0]→→→→→[5]★\n");

        var env = new RobotEnvironment();
        var policy = new SimpleQPolicy { Epsilon = 0.3 };
        var learner = new QLearner(policy);

        var agent = new SoftwareAgent<RobotState, RobotAction, RLStepResult, RLExperience>(
            perception: env,
            policy: policy,
            actuator: env,
            experienceBuilder: (state, action, result) =>
                new RLExperience(state, action, result.Reward, result.NextState),
            learner: learner,
            goalChecker: () => env.IsAtGoal
        );

        for (int step = 0; step < 8 && !agent.IsGoalReached; step++)
        {
            agent.Step();
            if (agent.IsGoalReached)
                Console.WriteLine("  [CILJ POSTIGNUT!]");
        }
    }
}
