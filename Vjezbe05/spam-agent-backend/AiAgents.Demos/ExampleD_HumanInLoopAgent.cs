/*
 * ════════════════════════════════════════════════════════════════════════════════
 *                     PRIMJER D: HUMAN-IN-THE-LOOP AGENT (Preporuka filmova)
 *                     SA DYNAMICPERCEPTION
 * ════════════════════════════════════════════════════════════════════════════════
 */

using System;
using System.Collections.Generic;
using System.Linq;
using AiAgents.Core;

namespace AiAgents.Demos.ConsoleApp;

/*
 * ┌─────────────────────────────────────────────────────────────────────────────┐
 * │  HUMAN-IN-THE-LOOP AGENT                                                   │
 * ├─────────────────────────────────────────────────────────────────────────────┤
 * │  • Politika = model + korisničke preferencije                              │
 * │  • Experience = (recommendation, userRating)                               │
 * │  • Feedback dolazi od ČOVJEKA                                              │
 * │                                                                             │
 * │  PRIMJER: Sistem za preporuku filmova SA DYNAMICPERCEPTION                 │
 * │           (simulacija kontinuiranog streaming servisa)                     │
 * └─────────────────────────────────────────────────────────────────────────────┘
 */

public readonly record struct UserProfile(List<string> LikedGenres);

public readonly record struct MovieRec(string Title, string Genre, double PredictedRating);

// Experience za human feedback
public readonly record struct HumanFeedbackExperience(MovieRec Recommendation, int UserRating);

// Politika: preporučuje film
public sealed class MovieRecommenderPolicy : IPolicy<UserProfile, MovieRec>
{
    private readonly List<(string title, string genre, double rating)> _movies = new()
    {
        ("Inception", "SciFi", 4.5),
        ("The Matrix", "Action", 4.8),
        ("Superbad", "Comedy", 3.9),
        ("Interstellar", "SciFi", 4.6),
        ("The Dark Knight", "Action", 4.9),
        ("Pulp Fiction", "Drama", 4.7),
        ("Forrest Gump", "Drama", 4.8),
        ("The Hangover", "Comedy", 4.0),
    };

    private readonly Dictionary<string, double> _genreBoost = new();
    private readonly HashSet<string> _recommendedMovies = new();

    public MovieRec SelectAction(UserProfile user)
    {
        var best = _movies
            .Where(m => !_recommendedMovies.Contains(m.title))  // Ne preporučuj već preporučene
            .Select(m => (m.title, m.genre, score: m.rating +
                (user.LikedGenres.Contains(m.genre) ? 0.5 : 0) +
                (_genreBoost.TryGetValue(m.genre, out var b) ? b : 0)))
            .OrderByDescending(x => x.score)
            .FirstOrDefault();

        if (best == default)
        {
            // Ako su svi filmovi već preporučeni, resetuj
            _recommendedMovies.Clear();
            best = _movies
                .Select(m => (m.title, m.genre, score: m.rating))
                .OrderByDescending(x => x.score)
                .First();
        }

        _recommendedMovies.Add(best.title);
        return new MovieRec(best.title, best.genre, best.score);
    }

    public void AdjustGenre(string genre, double delta)
    {
        if (!_genreBoost.ContainsKey(genre)) _genreBoost[genre] = 0;
        _genreBoost[genre] += delta;
    }
}

/*
 * DYNAMIC USER RATING SIMULATOR
 * ─────────────────────────────
 * Simulator koji DINAMIČKI vraća ocjenu za TRENUTNI film.
 * 
 * ZAŠTO JE OVO POTREBNO?
 * ─────────────────────
 * Kad koristimo DynamicPerception sa streaming kontekstom:
 *   • DynamicPerception dohvati korisnika/kontekst iz queue-a
 *   • DynamicUserRatingSimulator mora vratiti ocjenu za preporučeni film
 * 
 * U realnom sistemu, ovo bi bilo:
 *   • Korisnik gleda film i daje ocjenu
 *   • Implicitni feedback (gledao do kraja vs odustao)
 *   • Like/Dislike button
 */
public sealed class DynamicUserRatingSimulator : IActuator<MovieRec, int>
{
    private readonly Func<int> _ratingProvider;
    public DynamicUserRatingSimulator(Func<int> ratingProvider) => _ratingProvider = ratingProvider;
    public int Execute(MovieRec rec) => _ratingProvider();
}

// Učenje: na osnovu ocjene korisnika
public sealed class HumanFeedbackLearner : ILearningComponent<HumanFeedbackExperience>
{
    private readonly MovieRecommenderPolicy _policy;

    public HumanFeedbackLearner(MovieRecommenderPolicy policy)
        => _policy = policy ?? throw new ArgumentNullException(nameof(policy));

    public void Learn(HumanFeedbackExperience exp)
    {
        // Clamp rating to expected range (1..5) to avoid weird inputs
        int rating = Math.Max(1, Math.Min(5, exp.UserRating));

        // Convert rating into a small preference adjustment:
        // 1 -> -0.2, 2 -> -0.1, 3 -> 0.0, 4 -> +0.1, 5 -> +0.2
        double adjustment = (rating - 3) * 0.1;

        // Update internal preference for the genre
        _policy.AdjustGenre(exp.Recommendation.Genre, adjustment);

        // Terminal-friendly status (ASCII)
        string sentiment = rating >= 4 ? "[LIKE]" : rating <= 2 ? "[DISLIKE]" : "[NEUTRAL]";
        string sign = adjustment > 0 ? "+" : adjustment < 0 ? "-" : "0";

        Console.WriteLine($"  [MovieAgent] Recommendation: \"{exp.Recommendation.Title}\" ({exp.Recommendation.Genre})");
        Console.WriteLine($"  [MovieAgent] User rating: {rating}/5 {sentiment}  |  Genre boost: {sign}{Math.Abs(adjustment):0.0}");
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     DEMO METODA
// ════════════════════════════════════════════════════════════════════════════════

public static class MovieRecommenderDemo
{
    public static void Run()
    {
        /*
* ───────────────────────────────────────────────────────────────────────────────
*  DEMO: HUMAN-IN-THE-LOOP INTELIGENTNI AGENT
*        (Recommender sistem sa DynamicPerception – kontinuirani streaming servis)
* ───────────────────────────────────────────────────────────────────────────────
*
*  POČETNA INTUICIJA
*  ─────────────────────────────────────────
*  Studenti često kažu:
*    "Ovo je samo recommender sistem."
*
*  To je djelimično tačno – FUNKCIJSKI.
*  Ali ARHITEKTONSKI, ovo više NIJE samo recommender,
*  nego HUMAN-IN-THE-LOOP INTELIGENTNI AGENT.
*
*  Razlika nije u tome ŠTA sistem radi,
*  nego KAKO sistem funkcioniše.
*
*  ZAŠTO DYNAMICPERCEPTION?
*  ────────────────────────
*  U realnom streaming servisu:
*    • Korisnici dolaze i odlaze
*    • Profili se mijenjaju kroz vrijeme
*    • Kontekst gledanja varira (vikend vs radni dan, jutro vs večer)
*    • Agent mora kontinuirano opažati TRENUTNOG korisnika
*
*  DynamicPerception simulira ovaj realni tok:
*    • Queue korisnika/sessiona koji trebaju preporuku
*    • Svaki poziv Observe() vraća SLJEDEĆEG korisnika
*
*  RECOMMENDER vs AGENT (ključna razlika)
*  ──────────────────────────────────────
*  Recommender:
*    • generiše preporuku
*    • može biti statičan
*    • može raditi bez povratne informacije
*
*  Agent:
*    • opaža stanje okoline (percepcija)
*    • donosi odluku (policy)
*    • izvršava akciju
*    • uči iz povratne informacije
*
*  Ako ove četiri komponente postoje u petlji
*  (Sense → Think → Act → Learn),
*  govorimo o INTELIGENTNOM AGENTU.
*
*  ŠTA JE OKOLINA U OVOM PRIMJERU?
*  ─────────────────────────────
*  Okolina NIJE fizički svijet, nego:
*    • stream korisnika
*    • njihove ocjene
*    • implicitne preferencije
*
*  Okolina daje signal:
*    → da li je preporuka bila dobra ili loša
*
*  SHARED STATE PATTERN
*  ──────────────────────────────────────────────
*  Percepcija (userPerception) i Rating simulator (dynamicRating)
*  MORAJU biti sinhronizovani.
*
*  Zašto?
*    • Percepcija dohvati korisnika X
*    • Rating simulator mora vratiti ocjenu za film preporučen korisniku X
*
*  To rješavamo shared-state varijablom:
*    currentSession
*
*  GENERIC PARAMETRI AGENTA
*  ───────────────────────
*  TPercept    = UserProfile
*      → šta agent vidi: korisničke preferencije (žanrovi)
*
*  TAction     = MovieRec
*      → odluka agenta: koji film preporučiti
*
*  TResult     = int
*      → reakcija okoline: korisnička ocjena (1–5)
*
*  TExperience = HumanFeedbackExperience
*      → iskustvo za učenje:
*        (preporučeni film, korisnička ocjena)
*
*  KOMPONENTE AGENTA
*  ─────────────────
*
*  1) PERCEPCIJA (Sense)
*     - DynamicPerception<UserProfile>
*     Agent opaža kontinuirani tok korisnika.
*     Svaki poziv Observe() vraća SLJEDEĆEG korisnika iz queue-a.
*
*  2) POLITIKA (Think)
*     - MovieRecommenderPolicy
*     Politika kombinuje:
*       • osnovnu popularnost filma
*       • poklapanje žanra
*       • naučene preferencije (genre boost)
*
*     Politika ima STANJE koje se mijenja tokom učenja.
*
*  3) AKCIJA (Act)
*     - DynamicUserRatingSimulator
*     Agent izvršava akciju tako što:
*       • preporučuje film
*       • prima eksplicitni feedback korisnika
*
*     U realnom sistemu, ovo bi bio UI / mobilna aplikacija.
*
*  4) UČENJE (Learn)
*     - HumanFeedbackLearner
*     Agent koristi ljudski feedback da:
*       • pojača žanrove koji se sviđaju korisnicima
*       • oslabi žanrove koji se ne sviđaju
*
*     Nakon svakog feedbacka, agent NIJE isti kao prije.
*
*  AGENTIČKI CIKLUS (Sense → Think → Act → Learn)
*  ─────────────────────────────────────────────
*  Petlja prolazi kroz stream korisnika:
*
*    1) Agent vidi UserProfile (iz queue-a)
*    2) Odabere film (MovieRec)
*    3) Dobije ocjenu korisnika
*    4) Prilagodi buduće preporuke
*
*  Ovo je klasičan HUMAN-IN-THE-LOOP obrazac implementiran
*  kao kontinuirani backend proces.
*
*  ZAŠTO JE OVO AGENT, A NE "SAMO RECOMMENDER"?
*  ───────────────────────────────────────────
*    ✔ ima percepciju okoline (dinamički tok korisnika)
*    ✔ donosi odluke
*    ✔ interaguje sa okolinom
*    ✔ uči iz ljudskog feedbacka
*    ✔ radi kontinuirano (ne jednokratno)
*
*  Bez komponente učenja i kontinuiranog rada, ovo bi bio
*  običan recommender algoritam.
*
*  KLJUČNA REČENICA ZA STUDENTE
*  ───────────────────────────
*  "Recommender je postao agent onog trenutka
*   kada je dobio feedback-petlju, učenje i kontinuiranu percepciju."
*
* ───────────────────────────────────────────────────────────────────────────────
*/

        Console.WriteLine("\n┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│  4. HUMAN-IN-THE-LOOP: Preporuka filmova (DynamicPerception)│");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");

        // ─────────── SIMULACIJA USER SESSION QUEUE-a ───────────
        // U realnom sistemu ovo bi bio stream aktivnih korisnika
        var userSessionQueue = new Queue<(UserProfile profile, int rating)>(new[]
        {
            (new UserProfile(new List<string> { "SciFi", "Drama" }), 5),      // Korisnik 1: voli SciFi
            (new UserProfile(new List<string> { "Action", "Comedy" }), 2),    // Korisnik 2: ne voli preporuku
            (new UserProfile(new List<string> { "SciFi" }), 4),               // Korisnik 3: voli SciFi
            (new UserProfile(new List<string> { "Drama", "Action" }), 3),     // Korisnik 4: neutralan
            (new UserProfile(new List<string> { "Comedy" }), 5),              // Korisnik 5: voli Comedy
        });

        // ─────────── SHARED STATE ───────────
        // Trenutna sesija koju procesiramo (dijele je percepcija i rating simulator)
        (UserProfile profile, int rating) currentSession = default;

        // ─────────── DYNAMIC PERCEPTION ───────────
        // Svaki poziv Observe() vraća SLJEDEĆEG korisnika iz queue-a!
        var userPerception = new DynamicPerception<UserProfile>(() =>
        {
            if (userSessionQueue.Count > 0)
            {
                currentSession = userSessionQueue.Dequeue();
                Console.WriteLine($"  [Session] Novi korisnik, voli: [{string.Join(", ", currentSession.profile.LikedGenres)}]");
                return currentSession.profile;
            }
            return new UserProfile(new List<string>());
        });

        // ─────────── DYNAMIC RATING SIMULATOR ───────────
        // Simulator koji zna ocjenu za TRENUTNU sesiju (iz shared state)
        var dynamicRating = new DynamicUserRatingSimulator(() => currentSession.rating);

        var policy = new MovieRecommenderPolicy();
        var learner = new HumanFeedbackLearner(policy);

        // ─────────── JEDAN AGENT PROCESIRA SVE KORISNIKE ───────────
        var agent = new SoftwareAgent<UserProfile, MovieRec, int, HumanFeedbackExperience>(
            perception: userPerception,       // DynamicPerception - dohvaća iz queue-a
            policy: policy,
            actuator: dynamicRating,          // DynamicUserRatingSimulator - vraća ocjenu trenutnog
            experienceBuilder: (_, rec, r) => new HumanFeedbackExperience(rec, r),
            learner: learner
        );

        Console.WriteLine("  Agent procesira user session queue...\n");

        // Agent radi dok ima korisnika
        int totalSessions = userSessionQueue.Count;
        for (int i = 0; i <= totalSessions; i++)
        {
            agent.Step();
            Console.WriteLine();
        }

        Console.WriteLine("  [Session] Queue prazan - agent čeka nove korisnike...");
    }
}
