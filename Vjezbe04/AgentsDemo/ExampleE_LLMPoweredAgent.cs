/*
 * ════════════════════════════════════════════════════════════════════════════════
 *                     PRIMJER E: LLM-POWERED AGENT (Korisnička podrška)
 * ════════════════════════════════════════════════════════════════════════════════
 */

using System;
using System.Collections.Generic;
using AiAgents.Core;

namespace AiAgents.Examples.LLMPowered;

/*
 * ┌─────────────────────────────────────────────────────────────────────────────┐
 * │  LLM-POWERED AGENT                                                         │
 * ├─────────────────────────────────────────────────────────────────────────────┤
 * │  • Politika = LLM (GPT, Claude...)                                         │
 * │  • Experience = (response, supervisorFeedback)                             │
 * │  • Može učiti kroz fine-tuning, RAG, ili prompt engineering                │
 * │                                                                             │
 * │  PRIMJER: AI korisnička podrška SA DynamicPerception (ticket queue)        │
 * └─────────────────────────────────────────────────────────────────────────────┘
 */

public readonly record struct CustomerQuery(string Question, string Category, bool IsUrgent);

public readonly record struct AIResponse(string Answer, double Confidence, bool NeedsReview);

// Experience za LLM feedback
public readonly record struct LLMFeedbackExperience(
    CustomerQuery Query,
    AIResponse Response,
    bool WasGood,
    string Correction
);

// Politika: simulira LLM
public sealed class LLMSupportPolicy : IPolicy<CustomerQuery, AIResponse>
{
    private readonly Dictionary<string, string> _kb = new()
    {
        { "billing", "Za pitanja o računima, provjerite 'Moj račun'." },
        { "technical", "Pokušajte restartovati uređaj." },
        { "general", "Naš tim će vam uskoro odgovoriti." }
    };

    public AIResponse SelectAction(CustomerQuery query)
    {
        string answer = _kb.GetValueOrDefault(query.Category, _kb["general"]);
        double confidence = query.Category == "general" ? 0.6 : 0.85;
        return new AIResponse(answer, confidence, query.IsUrgent || confidence < 0.7);
    }
}

// STATIČKI Supervisor - vraća fiksni feedback
public sealed class SupervisorReview : IActuator<AIResponse, (bool wasGood, string correction)>
{
    private readonly bool _wasGood;
    private readonly string _correction;

    public SupervisorReview(bool wasGood, string correction = "")
    {
        _wasGood = wasGood;
        _correction = correction;
    }

    public (bool wasGood, string correction) Execute(AIResponse response) => (_wasGood, _correction);
}

/*
 * DYNAMIC SUPERVISOR ORACLE
 * ─────────────────────────
 * Supervisor koji DINAMIČKI vraća feedback za TRENUTNI tiket.
 * 
 * ZAŠTO JE OVO POTREBNO?
 * ─────────────────────
 * Kad koristimo DynamicPerception sa ticket queue-om:
 *   • DynamicPerception dohvati tiket iz queue-a
 *   • DynamicSupervisorOracle mora vratiti feedback za TAJ ISTI tiket
 * 
 * Oba koriste SHARED STATE (currentTicket) da budu sinkronizovani.
 * 
 * U realnom sistemu, supervisor bi bio:
 *   • Call-center operater koji pregleda AI odgovor
 *   • QA sistem koji ocjenjuje kvalitet
 *   • Korisnik koji ocjenjuje pomoć
 */
public sealed class DynamicSupervisorOracle : IActuator<AIResponse, (bool wasGood, string correction)>
{
    private readonly Func<(bool, string)> _feedbackProvider;
    public DynamicSupervisorOracle(Func<(bool, string)> feedbackProvider) => _feedbackProvider = feedbackProvider;
    public (bool wasGood, string correction) Execute(AIResponse response) => _feedbackProvider();
}

// Učenje: na osnovu feedback-a supervizora
public sealed class LLMFeedbackLearner : ILearningComponent<LLMFeedbackExperience>
{
    private int _good = 0;
    private int _total = 0;

    public void Learn(LLMFeedbackExperience exp)
    {
        _total++;
        if (exp.WasGood)
            _good++;

        // Siguran ispis pitanja (skraćeno)
        string questionPreview = exp.Query.Question ?? string.Empty;
        if (questionPreview.Length > 25)
            questionPreview = questionPreview.Substring(0, 25) + "...";

        // ASCII statusi (terminal-friendly)
        string reviewStatus = exp.Response.NeedsReview ? "[WARN]" : "[OK]";
        string supervisorStatus = exp.WasGood ? "[GOOD]" : "[BAD]";
        double quality = 100.0 * _good / _total;

        Console.WriteLine($"  [LLMAgent] Query: \"{questionPreview}\"");
        Console.WriteLine($"  [LLMAgent] Confidence: {exp.Response.Confidence:P0}, Review: {reviewStatus}");
        Console.WriteLine($"  [LLMAgent] Supervisor: {supervisorStatus}, Quality: {quality:F1}%");
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     DEMO METODA
// ════════════════════════════════════════════════════════════════════════════════

public static class CustomerSupportDemo
{
    public static void Run()
    {
        /*
         * ───────────────────────────────────────────────────────────────────────────────
         *  DEMO: LLM-POWERED INTELIGENTNI AGENT (DynamicPerception + Ticket Queue)
         *        "AI Customer Support" kao kontinuirani backend proces
         * ───────────────────────────────────────────────────────────────────────────────
         *
         *  POENTA DEMO-a
         *  ─────────────
         *  Ovaj primjer demonstrira LLM-POWERED SOFTVERSKOG AGENTA koji radi kao
         *  "ticket worker" u pozadini: uzima tikete iz queue-a, generiše odgovor,
         *  traži ljudski review kada treba i uči iz feedbacka.
         *
         *  Ključna nastavna poruka:
         *    • LLM nije agent.
         *    • LLM (ili ovdje njegova simulacija) je POLITIKA / decision module.
         *    • AGENT je cijeli Sense → Think → Act → Learn ciklus.
         *
         *  ZAŠTO DynamicPerception?
         *  ───────────────────────
         *  U realnim sistemima tiketi dolaze kontinuirano:
         *    • korisnici šalju zahtjeve u raznim trenucima
         *    • tiketi se stavljaju u queue
         *    • agent uzima sljedeći tiket kad je slobodan
         *
         *  To znači da percepcija nije statična vrijednost nego mehanizam:
         *    "daj mi sljedeći tiket iz okoline".
         *
         *  DynamicPerception simulira realni rad:
         *    Observe() → vrati SLJEDEĆI tiket iz queue-a
         *
         *  OVO JE RAZLIKA: "API endpoint" vs "Agent"
         *  ─────────────────────────────────────────
         *  API endpoint stil:
         *    • aplikacija pozove /ask
         *    • vrati odgovor
         *    • kraj
         *
         *  Agent stil (ovdje):
         *    • agent radi stalno u pozadini
         *    • sam uzima posao iz queue-a
         *    • ima vlastiti loop i stanje (metrike kvaliteta)
         *
         *  Time agent postaje prirodan "backend worker / service".
         *
         *  OKOLINA U OVOM PRIMJERU
         *  ──────────────────────
         *  Okolina nije fizička, nego poslovni sistem:
         *    • ticketQueue (simulirani tok posla)
         *    • supervizor koji ocjenjuje kvalitet
         *
         *  U realnosti:
         *    • ticketQueue = Zendesk/Jira queue, Kafka topic, RabbitMQ, SQL "pending" tabela
         *    • supervizor = agent-call centra koji pregleda odgovore, ispravlja i eskalira
         *
         *  SHARED STATE PATTERN (zašto je potreban?)
         *  ─────────────────────────────────────────
         *  Percepcija i supervizor moraju govoriti o ISTOM tiketu:
         *    • percepcija dohvati tiket X
         *    • supervizor mora vratiti feedback za tiket X
         *
         *  Zato koristimo shared-state varijablu:
         *    currentTicket
         *
         *  Ovaj pattern je čest kada:
         *    • više komponenti učestvuje u jednoj transakciji (isti posao/identitet)
         *    • u realnom sistemu bi to bila korelacija preko TicketId
         *
         *  GENERIC PARAMETRI AGENTA
         *  ───────────────────────
         *  TPercept    = CustomerQuery
         *      → šta agent vidi: upit korisnika + kategorija + hitnost
         *
         *  TAction     = AIResponse
         *      → šta agent proizvodi: odgovor + procjena sigurnosti + flag za review
         *
         *  TResult     = (bool wasGood, string correction)
         *      → reakcija okoline: ljudska procjena (good/bad) + korekcija
         *
         *  TExperience = LLMFeedbackExperience
         *      → iskustvo za učenje:
         *        (query, response, wasGood, correction)
         *
         *  KOMPONENTE AGENTA (Sense → Think → Act → Learn)
         *  ──────────────────────────────────────────────
         *
         *  1) PERCEPCIJA (Sense)
         *     - ticketPerception (DynamicPerception)
         *     Agent ne dobija unaprijed listu tiketa. On u petlji pita:
         *       "Ima li sljedeći tiket?"
         *
         *  2) POLITIKA (Think)
         *     - LLMSupportPolicy
         *     Ovo je simulacija LLM-a: bira odgovor na osnovu kategorije.
         *     U realnom sistemu, ovdje bi bio:
         *       • LLM poziv (GPT/Claude)
         *       • RAG (retrieval + generisanje)
         *       • prompt template + policy rules
         *
         *     Polje AIResponse.NeedsReview je primjer "gating" logike:
         *       • ako je tiket urgentan → review
         *       • ako je confidence nizak → review
         *
         *     To je industrijski standard: agent radi autonomno kada je siguran,
         *     a eskalira kada postoji rizik.
         *
         *  3) AKCIJA / OKOLINA (Act)
         *     - dynamicSupervisor (DynamicSupervisorOracle)
         *     U realnom sistemu, akcija bi bila:
         *       • poslati odgovor korisniku
         *       • ili otvoriti eskalaciju / dodijeliti čovjeku
         *
         *     Ovdje aktuator služi kao simulirani supervizor koji vraća:
         *       • da li je odgovor bio dobar
         *       • kako ga ispraviti (ako nije)
         *
         *  4) UČENJE (Learn)
         *     - LLMFeedbackLearner
         *     Ovdje učenje trenutno radi "light" stvar:
         *       • metrički prati kvalitet (good/total)
         *
         *     Ali struktura iskustva (LLMFeedbackExperience) je najvažnija,
         *     jer omogućava realno učenje u produkciji:
         *       • čuvanje korekcija kao training primjera
         *       • poboljšanje prompta
         *       • proširenje baze znanja
         *       • fine-tuning ili preference tuning
         *
         *  JEDAN AGENT PROCESIRA SVE TIKETE
         *  ────────────────────────────────
         *  Bitna razlika u odnosu na statičnu verziju:
         *    • ne pravimo novi agent za svaki tiket
         *    • jedan agent radi kontinuirano (worker proces)
         *    • agent akumulira metrike i "znanje" kroz vrijeme
         *
         *  PRODUKCIJSKA NAPOMENA (šta bi se dodalo u stvarnom sistemu)
         *  ───────────────────────────────────────────────────────────
         *  U praksi bi se obično dodalo:
         *    • TicketId i korelacija (umjesto shared-state varijable)
         *    • perzistencija (logovi, baza, audit trail)
         *    • rate limiting / retry / dead-letter queue
         *    • sigurnosna pravila (šta agent smije / ne smije odgovoriti)
         *    • multi-agent routing (billing vs technical timovi)
         *
         *  KLJUČNA REČENICA ZA STUDENTE
         *  ───────────────────────────
         *  "Ovo je agent zato što ne samo generiše odgovor,
         *   nego radi u petlji: opaža queue, odlučuje, eskalira po riziku,
         *   prima ljudski feedback i uči iz njega."
         *
         * ───────────────────────────────────────────────────────────────────────────────
         */


        Console.WriteLine("\n┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│  5. LLM-POWERED: AI podrška (DynamicPerception)             │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");

        // ─────────── SIMULACIJA TICKET QUEUE-a ───────────
        // U realnom sistemu ovo bi bio ticketing sistem (Zendesk, Jira, itd.)
        var ticketQueue = new Queue<(CustomerQuery query, bool wasGood, string correction)>(new[]
        {
            (new CustomerQuery("Zašto mi je račun veći ovaj mjesec?", "billing", false), true, ""),
            (new CustomerQuery("HITNO! Internet ne radi već 3 sata!!!", "technical", true), false, "Trebalo ponuditi tehničara"),
            (new CustomerQuery("Kako mogu promijeniti paket?", "general", false), true, ""),
            (new CustomerQuery("Greška pri plaćanju karticom", "billing", true), true, ""),
            (new CustomerQuery("Spor internet, plaćam za 100Mbit", "technical", false), false, "Trebalo provjeriti liniju"),
        });

        // ─────────── SHARED STATE ───────────
        // Trenutni tiket koji se procesira (dijele ga percepcija i supervisor)
        (CustomerQuery query, bool wasGood, string correction) currentTicket = default;

        // ─────────── DYNAMIC PERCEPTION ───────────
        // Svaki poziv Observe() vraća SLJEDEĆI tiket iz queue-a!
        var ticketPerception = new DynamicPerception<CustomerQuery>(() =>
        {
            if (ticketQueue.Count > 0)
            {
                currentTicket = ticketQueue.Dequeue();
                string urgentFlag = currentTicket.query.IsUrgent ? " [URGENT]" : "";
                Console.WriteLine($"  [Queue] Novi tiket:{urgentFlag} \"{currentTicket.query.Question}\"");
                return currentTicket.query;
            }
            return new CustomerQuery("(nema tiketa)", "general", false);
        });

        // ─────────── DYNAMIC SUPERVISOR ───────────
        // Supervisor koji zna feedback za TRENUTNI tiket (iz shared state)
        var dynamicSupervisor = new DynamicSupervisorOracle(() =>
            (currentTicket.wasGood, currentTicket.correction));

        var policy = new LLMSupportPolicy();
        var learner = new LLMFeedbackLearner();

        // ─────────── JEDAN AGENT PROCESIRA SVE TIKETE ───────────
        var agent = new SoftwareAgent<CustomerQuery, AIResponse, (bool, string), LLMFeedbackExperience>(
            perception: ticketPerception,     // DynamicPerception - dohvaća iz queue-a
            policy: policy,
            actuator: dynamicSupervisor,      // DynamicSupervisorOracle - vraća feedback trenutnog
            experienceBuilder: (q, resp, feedback) =>
                new LLMFeedbackExperience(q, resp, feedback.Item1, feedback.Item2),
            learner: learner
        );

        Console.WriteLine("  Agent procesira ticket queue...\n");

        // Agent radi dok ima tiketa
        int totalTickets = ticketQueue.Count;
        for (int i = 0; i <= totalTickets; i++)
        {
            agent.Step();
            Console.WriteLine();
        }

        Console.WriteLine("  [Queue] Svi tiketi procesirani - agent čeka nove...");
    }
}
