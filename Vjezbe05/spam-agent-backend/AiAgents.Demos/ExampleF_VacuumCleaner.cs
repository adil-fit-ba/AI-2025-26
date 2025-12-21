/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *          INTELIGENTNI USISIVAČ - IMPLEMENTACIJA SA GENERIČKOM ARHITEKTUROM
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 * Ovaj primjer pokazuje kako se Q-Learning usisivač uklapa u UNIVERZALNU
 * arhitekturu softverskih agenata (Sense → Think → Act → Learn).
 *
 * KLJUČNA IDEJA:
 * ──────────────
 * Usisivač je samo JEDNA INSTANCA generičkog SoftwareAgent-a sa:
 *
 *   • Percepcija = GridEnvironment (vraća VacuumState)
 *   • Politika   = QLearningPolicy (Q-tabela sa epsilon-greedy)
 *   • Aktuator   = GridActuator (pomjera agenta, čisti)
 *   • Učenje     = QLearningUpdater (Bellman update)
 *
 * Ista arhitektura bi radila za robota, igrača igara, chatbota...
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AiAgents.Core;

namespace AiAgents.Demos.ConsoleApp;

// ════════════════════════════════════════════════════════════════════════════════
//                     1. DOMENSKI TIPOVI (Usisivač)
// ════════════════════════════════════════════════════════════════════════════════

/*
 * ENUMERACIJE - tipski sigurne vrijednosti
 * ────────────────────────────────────────
 * Umjesto magičnih brojeva (-1, 0, 1) koristimo enum-e.
 * Kompajler provjerava ispravnost, a kod je čitljiviji.
 */

public enum TileState
{
    Wall = -1,    // Van granica mreže
    Clean = 0,    // Čista ćelija
    Dirty = 1     // Prljava ćelija
}

public enum VacuumAction
{
    Up = 0,       // Pomjeri se gore
    Down = 1,     // Pomjeri se dolje
    Left = 2,     // Pomjeri se lijevo
    Right = 3,    // Pomjeri se desno
    Clean = 4     // Očisti trenutnu ćeliju
}

/*
 * STANJE AGENTA (VacuumState)
 * ───────────────────────────
 * Ovo je "percepcija" usisivača - šta on "vidi" u svakom trenutku.
 *
 * Pamtimo poziciju + lokalnu okolinu (5 ćelija):
 *
 *           Up
 *           │
 *    Left ──●── Right
 *           │
 *         Down
 *      [Current]
 */
public readonly record struct VacuumState(
    int X, int Y,           // Pozicija agenta
    TileState Current,      // Stanje trenutne ćelije
    TileState Up,           // Stanje ćelije iznad
    TileState Down,         // Stanje ćelije ispod
    TileState Left,         // Stanje ćelije lijevo
    TileState Right         // Stanje ćelije desno
);

// Rezultat akcije za Vacuum: novo stanje + nagrada
public readonly record struct VacuumStepResult(VacuumState NextState, double Reward);

// Experience za Vacuum RL
public readonly record struct VacuumExperience(
    VacuumState State,
    VacuumAction Action,
    double Reward,
    VacuumState NextState
);

// ════════════════════════════════════════════════════════════════════════════════
//                     2. OKOLINA (Environment) - Implementira Percepciju i Aktuator
// ════════════════════════════════════════════════════════════════════════════════

/*
 * GRID VACUUM ENVIRONMENT
 * ───────────────────────
 * Okolina igra DVIJE uloge u RL:
 *
 * 1. PERCEPCIJA (IPerceptionSource) - agent pita "šta vidim?"
 * 2. AKTUATOR (IActuator) - agent kaže "uradi ovu akciju"
 *
 * Ovo je tipično za RL - okolina je i izvor percepcije i mjesto gdje
 * se akcije izvršavaju.
 */
public sealed class GridVacuumEnvironment :
    IPerceptionSource<VacuumState>,
    IActuator<VacuumAction, VacuumStepResult>
{
    private readonly Random _rng = new();
    private readonly int _size;
    private readonly double _dirtProb;

    private TileState[,] _grid = default!;
    private int _x, _y;

    // Za reward shaping (pomoćne nagrade)
    private HashSet<(int, int)> _visited = new();
    private VacuumAction? _lastAction;

    public GridVacuumEnvironment(int size = 6, double dirtProb = 0.3)
    {
        _size = size;
        _dirtProb = dirtProb;
        Reset();
    }

    // Javna svojstva za vizualizaciju
    public TileState[,] Grid => _grid;
    public (int X, int Y) AgentPosition => (_x, _y);
    public int Size => _size;

    // ─────────── RESET (nova epizoda) ───────────

    public VacuumState Reset()
    {
        _grid = new TileState[_size, _size];

        for (int i = 0; i < _size; i++)
            for (int j = 0; j < _size; j++)
                _grid[i, j] = _rng.NextDouble() < _dirtProb ? TileState.Dirty : TileState.Clean;

        _x = 0;
        _y = 0;
        _visited.Clear();
        _visited.Add((_x, _y));
        _lastAction = null;

        return Observe();
    }

    public bool HasNext => true; // okolina je uvijek "observable"; za kraj epizode koristiš goalChecker/maxSteps

    // ─────────── PERCEPCIJA (IPerceptionSource) ───────────

    /*
     * Agent pita: "Šta vidim?"
     * Okolina odgovara sa VacuumState (pozicija + 5 ćelija)
     */
    public VacuumState Observe()
    {
        TileState Tile(int i, int j) =>
            i >= 0 && i < _size && j >= 0 && j < _size
                ? _grid[i, j]
                : TileState.Wall;

        return new VacuumState(
            X: _x,
            Y: _y,
            Current: Tile(_x, _y),
            Up: Tile(_x - 1, _y),
            Down: Tile(_x + 1, _y),
            Left: Tile(_x, _y - 1),
            Right: Tile(_x, _y + 1)
        );
    }

    // ─────────── AKTUATOR (IActuator) ───────────

    /*
     * Agent kaže: "Uradi ovu akciju"
     * Okolina izvršava i vraća (novo_stanje, nagrada)
     */
    public VacuumStepResult Execute(VacuumAction action)
    {
        double reward = ExecuteAction(action);
        return new VacuumStepResult(Observe(), reward);
    }

    private double ExecuteAction(VacuumAction action)
    {
        int x = _x, y = _y;

        // ČIŠĆENJE
        if (action == VacuumAction.Clean)
        {
            if (_grid[x, y] == TileState.Dirty)
            {
                _grid[x, y] = TileState.Clean;
                return +50;  // Velika nagrada za čišćenje!
            }
            return -15;      // Kazna za čišćenje čiste ćelije
        }

        // KRETANJE
        int nx = x, ny = y;
        if (action == VacuumAction.Up) { nx = x - 1; ny = y; }
        if (action == VacuumAction.Down) { nx = x + 1; ny = y; }
        if (action == VacuumAction.Left) { nx = x; ny = y - 1; }
        if (action == VacuumAction.Right) { nx = x; ny = y + 1; }

        // Provjera granica
        if (!(nx >= 0 && nx < _size && ny >= 0 && ny < _size))
            return -10;  // Kazna za udaranje u zid

        // Pomjeri agenta
        _x = nx;
        _y = ny;

        // Izračunaj nagradu sa reward shaping
        double reward = -1;  // Bazna kazna po koraku

        if (_grid[nx, ny] == TileState.Dirty)
            reward += 6;  // Bonus za pronalazak prljavštine

        var pos = (nx, ny);
        if (_visited.Contains(pos))
            reward -= 3;  // Kazna za vraćanje
        else
        {
            reward += 3;  // Bonus za novu ćeliju
            _visited.Add(pos);
        }

        // Kazna za oscilaciju (gore-dolje, lijevo-desno)
        if (_lastAction.HasValue && IsOscillation(_lastAction.Value, action))
            reward -= 4;

        _lastAction = action;
        return reward;
    }

    private static bool IsOscillation(VacuumAction last, VacuumAction current) =>
        last == VacuumAction.Up && current == VacuumAction.Down ||
        last == VacuumAction.Down && current == VacuumAction.Up ||
        last == VacuumAction.Left && current == VacuumAction.Right ||
        last == VacuumAction.Right && current == VacuumAction.Left;

    // ─────────── PROVJERA CILJA ───────────

    public bool IsClean()
    {
        foreach (var tile in _grid)
            if (tile == TileState.Dirty)
                return false;
        return true;
    }

    public int CountDirt()
    {
        int count = 0;
        foreach (var tile in _grid)
            if (tile == TileState.Dirty)
                count++;
        return count;
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     3. Q-LEARNING POLITIKA (IPolicy)
// ════════════════════════════════════════════════════════════════════════════════

/*
 * Q-LEARNING POLICY
 * ─────────────────
 * Implementira IPolicy<VacuumState, VacuumAction>
 *
 * "Mozak" agenta - Q-tabela koja pamti očekivane nagrade za svako
 * stanje-akcija par.
 *
 * Koristi EPSILON-GREEDY strategiju:
 * • Sa vjerovatnoćom ε: nasumična akcija (exploration)
 * • Sa vjerovatnoćom 1-ε: najbolja poznata akcija (exploitation)
 */
public sealed class VacuumQLearningPolicy : IPolicy<VacuumState, VacuumAction>
{
    private readonly Random _rng = new();
    private readonly Dictionary<VacuumState, double[]> _qTable = new();

    private static readonly VacuumAction[] AllActions =
        (VacuumAction[])Enum.GetValues(typeof(VacuumAction));

    public double Epsilon { get; set; }

    /*
     * KONSTRUKTOR: Vacuum Q-Learning Policy (epsilon-greedy)
     * ─────────────────────────────────────────────────────
     *
     * Ovaj konstruktor inicijalizuje epsilon-greedy politiku
     * koja kontroliše ODNOS ISTRAŽIVANJA i ISKORIŠTAVANJA.
     *
     *   • epsilon (ε) – stopa istraživanja
     *
     * Epsilon određuje koliko često agent bira NASUMIČNU akciju
     * umjesto trenutno najbolje poznate akcije.
     *
     * ─────────────────────────────────────────────────────
     * ε (EPSILON) – ISTRAŽIVANJE vs ISKORIŠTAVANJE
     * ─────────────────────────────────────────────────────
     *
     * Tipične vrijednosti:
     *   • Donja granica: 0.0
     *   • Preporučeno:   0.05 – 0.3 (tokom treninga)
     *   • Gornja granica: 1.0
     *
     * Primjeri iz života:
     *
     *   • ε = 0.0  (bez istraživanja)
     *     → osoba uvijek radi ono što trenutno misli da je najbolje
     *     → nikad ne isprobava nove opcije
     *     → lako zapadne u lošu rutinu
     *
     *     U agentu:
     *       • nema istraživanja
     *       • agent može ostati u lokalnom optimumu
     *
     *   • ε ≈ 0.1 – 0.2  (uravnoteženo ponašanje)
     *     → osoba uglavnom koristi provjerene metode
     *     → ali povremeno isproba nešto novo
     *
     *     U agentu:
     *       • dobar balans između učenja i stabilnosti
     *       • najčešći izbor u praksi
     *
     *   • ε = 1.0  (potpuno nasumično ponašanje)
     *     → osoba svaki put radi nešto drugo
     *     → nikad ne gradi rutinu
     *     → nema dugoročnog plana
     *
     *     U agentu:
     *       • ponašanje je čisto nasumično
     *       • nema konvergencije
     *
     * Zaključak:
     *   Epsilon kontroliše KOLIKO je agent radoznao.
     *
     * ─────────────────────────────────────────────────────
     * PRAKTIČNE NAPOMENE
     * ─────────────────────────────────────────────────────
     *
     *  • Tokom treninga:
     *      epsilon je veći (npr. 0.2 – 0.3)
     *
     *  • Tokom eksploatacije (deployment):
     *      epsilon se smanjuje (npr. 0.0 – 0.05)
     *
     *  • Česta praksa:
     *      epsilon decay – postepeno smanjivanje epsilon-a
     *
     * ─────────────────────────────────────────────────────
     */
    public VacuumQLearningPolicy(double epsilon = 0.1)
    {
        Epsilon = epsilon;
    }

    // ─────────── ODABIR AKCIJE (IPolicy) ───────────

    /*
     * Agent pita: "Šta da uradim u ovom stanju?"
     * Politika odgovara sa akcijom (epsilon-greedy)
     */
    public VacuumAction SelectAction(VacuumState state)
    {
        // Exploration: nasumična akcija
        if (_rng.NextDouble() < Epsilon)
            return AllActions[_rng.Next(AllActions.Length)];

        // Exploitation: akcija sa najvećom Q-vrijednošću
        var qValues = GetQ(state);
        int bestIndex = ArgMax(qValues);
        return AllActions[bestIndex];
    }

    // ─────────── PRISTUP Q-TABELI ───────────

    public double[] GetQ(VacuumState state)
    {
        if (!_qTable.TryGetValue(state, out var qValues))
        {
            qValues = new double[AllActions.Length];  // Inicijalno sve 0
            _qTable[state] = qValues;
        }
        return qValues;
    }

    public void SetQ(VacuumState state, int actionIndex, double value)
    {
        GetQ(state)[actionIndex] = value;
    }

    private static int ArgMax(double[] arr)
    {
        int best = 0;
        for (int i = 1; i < arr.Length; i++)
            if (arr[i] > arr[best])
                best = i;
        return best;
    }

    public int StateCount => _qTable.Count;
}

// ════════════════════════════════════════════════════════════════════════════════
//                     4. Q-LEARNING UPDATER (ILearningComponent)
// ════════════════════════════════════════════════════════════════════════════════

/*
 * Q-LEARNING UPDATER
 * ──────────────────
 * Implementira ILearningComponent - komponenta koja ažurira znanje agenta.
 *
 * Koristi BELLMAN JEDNAČINU:
 * Q(s,a) = Q(s,a) + α * [R + γ * max(Q(s',a')) - Q(s,a)]
 *
 * Gdje je:
 * • α (alpha) = stopa učenja (koliko brzo usvajamo nova znanja)
 * • γ (gamma) = faktor diskontiranja (koliko cijenimo budućnost)
 * • R = trenutna nagrada
 * • s' = novo stanje
 */
public sealed class VacuumQLearningUpdater : ILearningComponent<VacuumExperience>
{
    private readonly VacuumQLearningPolicy _policy;
    private readonly double _alpha;
    private readonly double _gamma;

        /*
     * KONSTRUKTOR: Vacuum Q-Learning Updater
     * ──────────────────────────────────────
     *
     * Ovaj konstruktor inicijalizuje parametre učenja za Q-learning agenta.
     * Dva najvažnija parametra su:
     *
     *   • alpha (α)  – stopa učenja
     *   • gamma (γ)  – faktor diskontiranja budućih nagrada
     *
     * Ovi parametri direktno utiču na to:
     *   – koliko brzo agent mijenja svoje znanje
     *   – koliko "razmišlja unaprijed"
     *
     * ──────────────────────────────────────
     * α (ALPHA) – STOPA UČENJA
     * ──────────────────────────────────────
     * Alpha određuje KOLIKO NOVO ISKUSTVO utiče na postojeće znanje.
     *
     * Tipične vrijednosti:
     *   • Donja granica: 0.01 – 0.05
     *   • Preporučeno:   0.05 – 0.3
     *   • Gornja granica: 0.5 (rijetko više)
     *
     * Primjeri iz života:
     *
     *   • NIZAK alpha (npr. 0.01)
     *     → osoba koja sporo mijenja navike
     *     → treba joj mnogo ponavljanja da nauči
     *     → stabilna, ali spora adaptacija
     *
     *     U agentu:
     *       • učenje je stabilno
     *       • ali može trajati veoma dugo
     *
     *   • VISOK alpha (npr. 0.5 – 1.0)
     *     → osoba koja se odmah predomisli nakon jedne greške
     *     → lako "preuči" pogrešnu lekciju
     *
     *     U agentu:
     *       • učenje je nestabilno
     *       • Q-vrijednosti osciluju
     *       • agent može "zaboraviti" dobro znanje
     *
     * Zaključak:
     *   Alpha je kompromis između stabilnosti i brzine učenja.
     *
     * ──────────────────────────────────────
     * γ (GAMMA) – FAKTOR DISKONTIRANJA
     * ──────────────────────────────────────
     * Gamma određuje KOLIKO agent cijeni BUDUĆE nagrade
     * u odnosu na trenutnu nagradu.
     *
     * Tipične vrijednosti:
     *   • Donja granica: 0.0 – 0.3
     *   • Preporučeno:   0.9 – 0.99
     *   • Gornja granica: 1.0 (teorijski maksimum)
     *
     * Primjeri iz života:
     *
     *   • NIZAK gamma (npr. 0.1)
     *     → "kratkovida" osoba
     *     → bira brzu nagradu sada
     *     → ne planira dugoročno
     *
     *     U agentu:
     *       • fokus na trenutnu nagradu
     *       • često bira suboptimalne puteve
     *
     *   • VISOK gamma (npr. 0.95 – 0.99)
     *     → osoba koja planira unaprijed
     *     → spremna je da trpi mali gubitak sada
     *       radi veće koristi kasnije
     *
     *     U agentu:
     *       • uči dugoročne strategije
     *       • idealno za navigaciju i planiranje
     *
     * Zaključak:
     *   Gamma kontroliše koliko daleko u budućnost agent "gleda".
     *
     * ──────────────────────────────────────
     * PRAKTIČNA PREPORUKA ZA VACUUM AGENTA
     * ──────────────────────────────────────
     *   • alpha ≈ 0.1  → umjereno, stabilno učenje
     *   • gamma ≈ 0.9  → agent planira više koraka unaprijed
     *
     * Ove vrijednosti daju dobar balans između:
     *   • brzine učenja
     *   • stabilnosti
     *   • dugoročnog ponašanja
     *
     * ──────────────────────────────────────
     */
    public VacuumQLearningUpdater(VacuumQLearningPolicy policy, double alpha = 0.1, double gamma = 0.9)
    {
        _policy = policy;
        _alpha = alpha;
        _gamma = gamma;
    }

    /*
     * Agent kaže: "Naučio sam ovo iskustvo"
     * Updater ažurira Q-tabelu
     */
    public void Learn(VacuumExperience exp)
    {
        var q = _policy.GetQ(exp.State);
        var nextQ = _policy.GetQ(exp.NextState);

        int a = (int)exp.Action;

        // Bellman update - UVIJEK dodaje buduće nagrade (kao Python original)
        double target = exp.Reward + _gamma * nextQ.Max();
        double tdError = target - q[a];
        q[a] = q[a] + _alpha * tdError;
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     5. VACUUM AGENT - NASLJEĐUJE SoftwareAgent
// ════════════════════════════════════════════════════════════════════════════════

/*
 * VACUUM CLEANING AGENT
 * ─────────────────────
 * Sada NASLJEĐUJE SoftwareAgent<> umjesto da direktno implementira IAgent!
 *
 * Ovo je ISTA struktura kao bilo koji drugi agent:
 *
 *   ┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
 *   │ GridEnvironment │ ──► │ QLearningPolicy │ ──► │ GridEnvironment │
 *   │   (Percepcija)  │     │    (Politika)   │     │   (Aktuator)    │
 *   └─────────────────┘     └─────────────────┘     └─────────────────┘
 *            │                       ▲                       │
 *            │                       │                       │
 *            │               ┌───────┴───────┐               │
 *            └──────────────►│QLearningUpdate│◄──────────────┘
 *                            │   (Učenje)    │
 *                            └───────────────┘
 */
public sealed class VacuumCleaningAgent : SoftwareAgent<VacuumState, VacuumAction, VacuumStepResult, VacuumExperience>
{
    private readonly GridVacuumEnvironment _environment;
    private readonly VacuumQLearningPolicy _qPolicy;

    public VacuumCleaningAgent(
        GridVacuumEnvironment environment,
        VacuumQLearningPolicy policy,
        VacuumQLearningUpdater learner)
        : base(
            perception: environment,
            policy: policy,
            actuator: environment,
            experienceBuilder: (state, action, result) =>
                new VacuumExperience(state, action, result.Reward, result.NextState),
            learner: learner,
            goalChecker: () => environment.IsClean()
        )
    {
        _environment = environment;
        _qPolicy = policy;
    }

    // Pristup komponentama za statistiku i vizualizaciju
    public VacuumQLearningPolicy Policy => _qPolicy;
    public GridVacuumEnvironment Environment => _environment;
}

// ════════════════════════════════════════════════════════════════════════════════
//                     6. TRENING I VIZUALIZACIJA
// ════════════════════════════════════════════════════════════════════════════════

public static class VacuumTraining
{
    public static void Train(VacuumCleaningAgent agent, int episodes = 3500, int maxSteps = 400)
    {
        var env = agent.Environment;

        for (int ep = 0; ep < episodes; ep++)
        {
            env.Reset();

            for (int t = 0; t < maxSteps; t++)
            {
                agent.Step();

                if (agent.IsGoalReached)
                    break;
            }

            if ((ep + 1) % 500 == 0)
            {
                Console.WriteLine($"  Epizoda {ep + 1}/{episodes}, Q-stanja: {agent.Policy.StateCount}");
            }
        }

        // Isključi exploration nakon treninga
        agent.Policy.Epsilon = 0;
    }
}

public static class VacuumVisualizer
{
    private const char AGENT = 'O';
    private const char DIRT = '#';
    private const char CLEAN = '.';

    public static void Visualize(VacuumCleaningAgent agent, int delayMs = 250)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var env = agent.Environment;
        env.Reset();

        int steps = 0;
        int cleaned = 0;
        int initialDirt = env.CountDirt();
        int maxSteps = 300;

        while (!agent.IsGoalReached && steps < maxSteps)
        {
            Console.Clear();
            DrawFrame(env, steps, cleaned, initialDirt);

            // Agent radi jedan korak
            var stateBefore = env.Observe();
            agent.Step();
            var stateAfter = env.Observe();

            // Provjeri da li je očistio
            if (stateBefore.Current == TileState.Dirty && stateAfter.Current == TileState.Clean)
                cleaned++;

            steps++;
            Thread.Sleep(delayMs);
        }

        // Završni prikaz
        Console.Clear();
        DrawFrame(env, steps, cleaned, initialDirt);

        if (agent.IsGoalReached)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[OK] CISCENJE ZAVRSENO u {steps} koraka!");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n[!] Dostignut maksimalan broj koraka");
        }
        Console.ResetColor();
    }

    private static void DrawFrame(GridVacuumEnvironment env, int steps, int cleaned, int initial)
    {
        int size = env.Size;
        var (ax, ay) = env.AgentPosition;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("+=======================================+");
        Console.WriteLine("|     INTELIGENTNI USISIVAC (Q-RL)      |");
        Console.WriteLine("|   Genericka Agent Arhitektura Demo    |");
        Console.WriteLine("+=======================================+\n");
        Console.ResetColor();

        // Gornji okvir
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  +");
        Console.Write(new string('-', size * 3));
        Console.WriteLine("+");

        // Mreža
        for (int i = 0; i < size; i++)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  |");

            for (int j = 0; j < size; j++)
            {
                bool isAgent = i == ax && j == ay;

                if (isAgent)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Write($" {AGENT} ");
                }
                else if (env.Grid[i, j] == TileState.Dirty)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write($" {DIRT} ");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($" {CLEAN} ");
                }
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("|");
        }

        // Donji okvir
        Console.Write("  +");
        Console.Write(new string('-', size * 3));
        Console.WriteLine("+");
        Console.ResetColor();

        // Statistika
        Console.WriteLine();
        Console.WriteLine($"  Koraci: {steps,-5} Ocisceno: {cleaned}/{initial}  Preostalo: {env.CountDirt()}");

        // Legenda
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("\n  Legenda: ");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write($"{AGENT}=Agent ");
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write($"{DIRT}=Prljavstina ");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"{CLEAN}=Cisto");
        Console.ResetColor();
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     7. DEMO METODA
// ════════════════════════════════════════════════════════════════════════════════

public static class VacuumCleanerDemo
{
    public static void Run()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("+===============================================================+");
        Console.WriteLine("|        Q-LEARNING USISIVAC - GENERICKA ARHITEKTURA            |");
        Console.WriteLine("+===============================================================+");
        Console.WriteLine("|  Ovaj agent koristi ISTU arhitekturu kao svi drugi agenti:    |");
        Console.WriteLine("|                                                               |");
        Console.WriteLine("|    Percepcija --> Politika --> Aktuator                       |");
        Console.WriteLine("|         |             ^            |                          |");
        Console.WriteLine("|         +------> Ucenje <----------+                          |");
        Console.WriteLine("|                                                               |");
        Console.WriteLine("|  Komponente:                                                  |");
        Console.WriteLine("|  - Percepcija: GridVacuumEnvironment.Observe()                |");
        Console.WriteLine("|  - Politika:   QLearningPolicy (Q-tabela + e-greedy)          |");
        Console.WriteLine("|  - Aktuator:   GridVacuumEnvironment.Execute()                |");
        Console.WriteLine("|  - Ucenje:     QLearningUpdater (Bellman update)              |");
        Console.WriteLine("+===============================================================+");
        Console.ResetColor();
        Console.WriteLine();

        // ─────────── KREIRANJE KOMPONENTI ───────────

        // 1. Okolina (služi kao Percepcija + Aktuator)
        var environment = new GridVacuumEnvironment(size: 6, dirtProb: 0.3);

        // 2. Politika (Q-Learning sa epsilon-greedy)
        var policy = new VacuumQLearningPolicy(epsilon: 0.1);

        // 3. Učenje (Q-Learning updater)
        var learner = new VacuumQLearningUpdater(policy, alpha: 0.1, gamma: 0.9);

        // 4. AGENT = kompozicija komponenti (sada nasljeđuje SoftwareAgent!)
        var agent = new VacuumCleaningAgent(environment, policy, learner);

        // ─────────── TRENING ───────────

        Console.WriteLine("Training agent...");
        VacuumTraining.Train(agent, episodes: 3500);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[OK] Trening zavrsen! Q-stanja: {policy.StateCount}");
        Console.ResetColor();

        // ─────────── VIZUALIZACIJA ───────────

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n>> Pritisni ENTER za vizualizaciju...");
        Console.ResetColor();
        Console.ReadLine();

        Console.WriteLine("Visualizing...");
        VacuumVisualizer.Visualize(agent);

        Console.WriteLine("\n>> Pritisni ENTER za povratak u meni...");
        Console.ReadLine();
    }
}

/*
 * ═══════════════════════════════════════════════════════════════════════════════
 *                              BILJEŠKE ZA STUDENTE
 * ═══════════════════════════════════════════════════════════════════════════════
 *
 * ZAŠTO JE OVO VAŽNO?
 * ───────────────────
 * Ovaj primjer pokazuje da je Q-Learning usisivač SAMO JEDNA INSTANCA
 * generičke arhitekture softverskih agenata.
 *
 * Ista struktura radi za:
 * • Robota koji vozi autonomno
 * • Chatbota koji odgovara na pitanja
 * • Igrača koji igra šah
 * • Sistem koji preporučuje filmove
 *
 * RAZLIKA JE SAMO U IMPLEMENTACIJI KOMPONENTI:
 *
 * ┌─────────────┬──────────────────┬──────────────────┬──────────────────┐
 * │   Agent     │    Percepcija    │     Politika     │     Učenje       │
 * ├─────────────┼──────────────────┼──────────────────┼──────────────────┤
 * │ Usisivač    │ GridEnvironment  │ Q-Learning       │ Bellman Update   │
 * │ Chatbot     │ UserMessage      │ LLM (GPT/Claude) │ Fine-tuning/RAG  │
 * │ Šah igrač   │ BoardState       │ Minimax/MCTS     │ Self-play        │
 * │ Termostat   │ TemperatureSensor│ IF-THEN pravila  │ Nema (rule-based)│
 * └─────────────┴──────────────────┴──────────────────┴──────────────────┘
 *
 * NOVA ARHITEKTURA:
 * ─────────────────
 * VacuumCleaningAgent sada NASLJEĐUJE SoftwareAgent<> umjesto da
 * direktno implementira IAgent. Time se postiže:
 *
 * • Konzistentnost sa drugim primjerima
 * • Ponovna upotrebljivost bazne klase
 * • Lakše proširivanje i održavanje
 */
