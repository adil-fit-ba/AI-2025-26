/*
 * ════════════════════════════════════════════════════════════════════════════════
 *                     PRIMJER B: SUPERVISED LEARNING AGENT (Spam Detektor)
 * ════════════════════════════════════════════════════════════════════════════════
 */

using System;
using System.Collections.Generic;
using AiAgents.Core;

namespace AiAgents.Demos.ConsoleApp;

/*
 * ┌─────────────────────────────────────────────────────────────────────────────┐
 * │  SUPERVISED LEARNING AGENT                                                 │
 * ├─────────────────────────────────────────────────────────────────────────────┤
 * │  • Politika = naučeni ML model                                             │
 * │  • Uči iz LABELIRANIH primjera (input → correct_output)                   │
 * │  • Experience = (predicted, actual)                                        │
 * │                                                                             │
 * │  PRIMJER: Spam detektor SA DynamicPerception (inbox queue)                 │
 * └─────────────────────────────────────────────────────────────────────────────┘
 */

public readonly record struct EmailFeatures(
    string Subject,
    int LinkCount,
    double SuspiciousWordRatio
);

public enum EmailClass { NotSpam, Spam }

// Experience za supervised learning: šta smo predvidjeli vs šta je tačno
public readonly record struct SupervisedExperience(
    EmailFeatures Input,
    EmailClass Predicted,
    EmailClass Actual
);

// Politika: jednostavan "model"
public sealed class SpamClassifierPolicy : IPolicy<EmailFeatures, EmailClass>
{
    private double _threshold = 0.4;

    public EmailClass SelectAction(EmailFeatures email)
    {
        double score = Math.Min(email.LinkCount * 0.1, 0.3) + email.SuspiciousWordRatio * 0.5;
        return score > _threshold ? EmailClass.Spam : EmailClass.NotSpam;
    }

    public void AdjustThreshold(double delta) => _threshold += delta;
}

// STATIČKI Oracle - vraća fiksnu labelu (za jednostavne slučajeve)
public sealed class SpamLabelOracle : IActuator<EmailClass, EmailClass>
{
    private readonly EmailClass _trueLabel;
    public SpamLabelOracle(EmailClass trueLabel) => _trueLabel = trueLabel;
    public EmailClass Execute(EmailClass predicted) => _trueLabel;
}

/*
 * DYNAMIC SPAM ORACLE
 * ───────────────────
 * Oracle koji DINAMIČKI vraća tačnu labelu za TRENUTNI email.
 * 
 * ZAŠTO JE OVO POTREBNO?
 * ─────────────────────
 * Kad koristimo DynamicPerception sa inbox queue-om:
 *   • DynamicPerception dohvati email iz queue-a
 *   • DynamicSpamOracle mora vratiti labelu za TAJ ISTI email
 * 
 * Oba koriste SHARED STATE (currentEmail) da budu sinkronizovani.
 * 
 *
 * NAPOMENA (termin "Oracle"):
 * ──────────────────────────
 * Riječ "oracle" se u AI i Machine Learning literaturi koristi
 * za komponentu OKOLINE koja zna TAČAN ODGOVOR (ground truth).
 *
 * Oracle:
 *   • NE donosi odluke
 *   • NE uči
 *   • NE predviđa
 *
 * Njegina jedina uloga je da agentu kaže:
 *   "Ovo je stvarna, ispravna vrijednost."
 *
 * U supervised learningu, oracle je neophodan
 * jer omogućava poređenje:
 *   predicted vs actual
 *
 * U realnim sistemima, oracle može biti:
 *   • korisnik (klikne "spam / not spam")
 *   • moderator
 *   • doktor (u medicinskim sistemima)
 *   • historijski labelirani podaci
 *
 * U ovom demo-u, oracle je simuliran klasom
 * koja vraća tačnu labelu za trenutno opaženi email.
 */

public sealed class DynamicSpamOracle : IActuator<EmailClass, EmailClass>
{
    private readonly Func<EmailClass> _labelProvider;
    public DynamicSpamOracle(Func<EmailClass> labelProvider) => _labelProvider = labelProvider;
    public EmailClass Execute(EmailClass predicted) => _labelProvider();
}

// Učenje: ažurira model na osnovu grešaka
public sealed class SupervisedLearner : ILearningComponent<SupervisedExperience>
{
    private readonly SpamClassifierPolicy _policy;
    private int _correct = 0, _total = 0;

    public SupervisedLearner(SpamClassifierPolicy policy) => _policy = policy;

    public void Learn(SupervisedExperience exp)
    {
        _total++;
        if (exp.Predicted == exp.Actual)
            _correct++;
        else
            _policy.AdjustThreshold(0.01 * (new Random().NextDouble() > 0.5 ? 1 : -1));

        Console.WriteLine($"  [SpamAgent] Predicted: {exp.Predicted}, Actual: {exp.Actual}, " +
                          $"Accuracy: {100.0 * _correct / _total:F1}%");
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     DEMO METODA
// ════════════════════════════════════════════════════════════════════════════════

public static class SpamDetectorDemo
{
    public static void Run()
    {
        /*
       * ───────────────────────────────────────────────────────────────────────────────
       *  DEMO: SUPERVISED LEARNING INTELIGENTNI AGENT
       *        (Spam detektor sa DynamicPerception – realni backend scenarij)
       * ───────────────────────────────────────────────────────────────────────────────
       *
       *  POENTA DEMO-a
       *  ─────────────
       *  Ovaj primjer demonstrira SUPERVISED LEARNING AGENTA koji radi
       *  kao STALNO AKTIVAN BACKEND PROCES, a ne kao jednokratna funkcija.
       *
       *  Ovo je ključna razlika između:
       *    • ML algoritma
       *    • i inteligentnog agenta
       *
       *  Agent ne dobija unaprijed kompletan dataset.
       *  Agent opaža okolinu KONTINUIRANO i reaguje u realnom vremenu.
       *
       *  ZAŠTO DynamicPerception?
       *  ────────────────────────
       *  U realnim sistemima (email, logovi, eventi):
       *    • podaci NE DOLAZE odjednom
       *    • podaci dolaze u TOKU (stream / queue)
       *    • agent ne zna unaprijed šta će sljedeće dobiti
       *
       *  Zato je percepcija:
       *    → FUNKCIJA nad okruženjem
       *    → a ne fiksna vrijednost
       *
       *  DynamicPerception prima lambda-funkciju koja se poziva svaki put
       *  kada agent treba novu percepciju.
       *
       *  ANALOGIJA IZ STVARNOG SVIJETA:
       *  ─────────────────────────────
       *  StaticPerception:
       *    „Evo ti ovaj email, obradi ga."
       *
       *  DynamicPerception:
       *    „Pogledaj inbox – šta ima novo?"
       *
       *  OKOLINA U OVOM PRIMJERU
       *  ──────────────────────
       *  Okolina je simulirani EMAIL INBOX, implementiran kao Queue.
       *
       *  U realnom sistemu, ovo bi bilo:
       *    • IMAP / SMTP server
       *    • message broker (RabbitMQ, Kafka)
       *    • event stream
       *    • REST API koji vraća nove poruke
       *
       *  SHARED STATE PATTERN (važna arhitektonska poenta)
       *  ────────────────────────────────────────────────
       *  Percepcija (inboxPerception) i Oracle (dynamicOracle)
       *  MORAJU biti sinhronizovani.
       *
       *  Zašto?
       *    • Percepcija povlači email X iz queue-a
       *    • Oracle mora vratiti TAČNU labelu za ISTI email X
       *
       *  To rješavamo shared-state varijablom:
       *    currentEmail
       *
       *  Ovo je realan pattern u agentičkim sistemima gdje:
       *    • različite komponente gledaju isto stanje okoline
       *
       *  GENERIC PARAMETRI AGENTA
       *  ───────────────────────
       *  TPercept    = EmailFeatures
       *      → šta agent vidi: feature-reprezentaciju emaila
       *
       *  TAction     = EmailClass
       *      → odluka agenta: Spam ili NotSpam
       *
       *  TResult     = EmailClass
       *      → odgovor okoline: stvarna (tačna) labela
       *
       *  TExperience = SupervisedExperience
       *      → iskustvo za učenje:
       *        (input, predicted, actual)
       *
       *  KOMPONENTE AGENTA
       *  ─────────────────
       *
       *  1) PERCEPCIJA (Sense)
       *     - inboxPerception (DynamicPerception)
       *     Agent svaki put pita:
       *       „Ima li novi email?"
       *
       *     Ako ima:
       *       • email se preuzima iz queue-a
       *       • agent dobija nove podatke
       *
       *     Ako nema:
       *       • agent bi u realnom sistemu čekao (idle)
       *
       *  2) POLITIKA (Think)
       *     - SpamClassifierPolicy
       *     Politika donosi odluku na osnovu trenutnih parametara modela.
       *
       *     Bitno:
       *     Politika je STATEFUL – njeno ponašanje se mijenja kroz učenje.
       *
       *  3) AKCIJA + OKOLINA (Act)
       *     - dynamicOracle
       *     Agent dobija povratnu informaciju iz okoline:
       *       • da li je odluka bila tačna
       *
       *     U realnom sistemu:
       *       • korisnik označi spam
       *       • moderator ispravi grešku
       *       • historijski labelirani podaci služe kao oracle
       *
       *  4) UČENJE (Learn)
       *     - SupervisedLearner
       *     Agent koristi grešku (predicted vs actual) da:
       *       • prilagodi parametre politike
       *       • poboljša buduće odluke
       *
       *  AGENTIČKI CIKLUS (Sense → Think → Act → Learn)
       *  ─────────────────────────────────────────────
       *  Agent radi u PETLJI:
       *
       *    1) opaža inbox (DynamicPerception)
       *    2) klasificira email
       *    3) dobija feedback
       *    4) uči iz iskustva
       *
       *  Ovaj ciklus se ponavlja za svaki novi email.
       *
       *  ZAŠTO JE OVO PRAVI AGENT?
       *  ────────────────────────
       *    ✔ agent opaža promjenjivu okolinu
       *    ✔ agent donosi odluke u realnom vremenu
       *    ✔ agent interaguje sa okolinom
       *    ✔ agent uči iz povratne informacije
       *    ✔ agent radi kontinuirano, ne jednokratno
       *
       *  Bez DynamicPerception, ovo bi bio:
       *    • batch ML primjer
       *
       *  Sa DynamicPerception, ovo je:
       *    • dugotrajni softverski agent (daemon / service)
       *
       *  KLJUČNA REČENICA ZA STUDENTE
       *  ───────────────────────────
       *  „Agent nije funkcija koja se pozove jednom.
       *   Agent je proces koji stalno opaža, odlučuje i uči."
       *
       * ───────────────────────────────────────────────────────────────────────────────
       */


        Console.WriteLine("\n┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│  2. SUPERVISED LEARNING: Spam detektor (DynamicPerception)  │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");

        // ─────────── SIMULACIJA INBOX QUEUE-a ───────────
        // U realnom sistemu ovo bi bio IMAP klijent, message broker, ili API
        var inboxQueue = new Queue<(EmailFeatures email, EmailClass trueLabel)>(new[]
        {
            (new EmailFeatures("Meeting tomorrow at 10am", 0, 0.05), EmailClass.NotSpam),
            (new EmailFeatures("CONGRATULATIONS! You WON $1000000!!!", 7, 0.95), EmailClass.Spam),
            (new EmailFeatures("Your invoice #12345", 1, 0.15), EmailClass.NotSpam),
            (new EmailFeatures("FREE VIAGRA CHEAP!!!", 12, 0.99), EmailClass.Spam),
            (new EmailFeatures("Project update - Q4 results", 2, 0.10), EmailClass.NotSpam),
            (new EmailFeatures("Odbrana RS1", 2, 0.80), EmailClass.NotSpam),
        });

        // ─────────── SHARED STATE ───────────
        // Trenutni email koji se procesira (dijele ga percepcija i oracle)
        (EmailFeatures email, EmailClass label) currentEmail = default;

        // ─────────── DYNAMIC PERCEPTION ───────────
        // Svaki poziv Observe() vraća SLJEDEĆI email iz queue-a!
        var inboxPerception = new DynamicPerception<EmailFeatures>(() =>
        {
            if (inboxQueue.Count > 0)
            {
                currentEmail = inboxQueue.Dequeue();
                Console.WriteLine($"  [Inbox] Novi email: \"{currentEmail.email.Subject}\"");
                return currentEmail.email;
            }
            return new EmailFeatures("(prazan inbox)", 0, 0);
        });

        // ─────────── DYNAMIC ORACLE ───────────
        // Oracle koji zna tačnu labelu za TRENUTNI email (iz shared state)
        var dynamicOracle = new DynamicSpamOracle(() => currentEmail.label);

        var policy = new SpamClassifierPolicy();
        var learner = new SupervisedLearner(policy);

        // ─────────── JEDAN AGENT PROCESIRA SVE EMAILOVE ───────────
        var agent = new SoftwareAgent<EmailFeatures, EmailClass, EmailClass, SupervisedExperience>(
            perception: inboxPerception,      // DynamicPerception - dohvaća iz queue-a
            policy: policy,
            actuator: dynamicOracle,          // DynamicSpamOracle - vraća labelu trenutnog
            experienceBuilder: (input, predicted, actual) => new SupervisedExperience(input, predicted, actual),
            learner: learner
        );

        Console.WriteLine("  Agent procesira inbox queue...\n");

        // Agent radi dok ima emailova
        int totalEmails = inboxQueue.Count;
        for (int i = 0; i <= totalEmails; i++)
        {
            agent.Step();
            Console.WriteLine();
        }

        Console.WriteLine("  [Inbox] Queue prazan - agent čeka nove emailove...");
    }
}
