/*
 * ════════════════════════════════════════════════════════════════════════════════
 *                     PRIMJER A: RULE-BASED AGENT (Termostat)
 * ════════════════════════════════════════════════════════════════════════════════
 */

using System;
using AiAgents.Core;

namespace AiAgents.Demos.ConsoleApp;

/*
 * ┌─────────────────────────────────────────────────────────────────────────────┐
 * │  RULE-BASED AGENT                                                          │
 * ├─────────────────────────────────────────────────────────────────────────────┤
 * │  • Politika = skup IF-THEN pravila                                         │
 * │  • NE UČI - pravila su fiksna                                              │
 * │  • Nema "cilj" - radi beskonačno                                           │
 * │                                                                             │
 * │  PRIMJER: Pametni termostat                                                │
 * └─────────────────────────────────────────────────────────────────────────────┘
 */

public readonly record struct TemperatureReading(double Celsius);

public enum ThermostatAction { HeatOn, HeatOff, CoolOn, CoolOff }

// Politika: IF-THEN pravila
public sealed class ThermostatPolicy : IPolicy<TemperatureReading, ThermostatAction>
{
    private readonly double _target;
    private readonly double _tolerance;

    public ThermostatPolicy(double target = 21.0, double tolerance = 1.0)
    {
        _target = target;
        _tolerance = tolerance;
    }

    public ThermostatAction SelectAction(TemperatureReading reading)
    {
        if (reading.Celsius < _target - _tolerance)
            return ThermostatAction.HeatOn;
        if (reading.Celsius > _target + _tolerance)
            return ThermostatAction.CoolOn;
        return ThermostatAction.HeatOff;
    }
}

// Simulirani senzor
public sealed class TemperatureSensor : IPerceptionSource<TemperatureReading>
{
    private readonly Random _rng = new();
    private double _temp;

    public TemperatureSensor(double initial = 18.0) => _temp = initial;

    public bool HasNext => true;

    public TemperatureReading Observe()
    {
        _temp += (_rng.NextDouble() - 0.5) * 2;  // Mala nasumična promjena
        return new TemperatureReading(_temp);
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//                     DEMO METODA
// ════════════════════════════════════════════════════════════════════════════════

public static class ThermostatDemo
{
    public static void Run()
    {
        /*
 * ───────────────────────────────────────────────────────────────────────────────
 *  DEMO: RULE-BASED INTELIGENTNI AGENT
 *        (Pametni termostat – bez učenja)
 * ───────────────────────────────────────────────────────────────────────────────
 *
 *  POENTA DEMO-a
 *  ─────────────
 *  Ovaj primjer demonstrira NAJOSNOVNIJI oblik inteligentnog agenta:
 *  RULE-BASED (reaktivni) agent.
 *
 *  Agent reaguje isključivo na TRENUTNO stanje okoline koristeći
 *  unaprijed definisana IF–THEN pravila.
 *
 *  Ne postoji:
 *    • učenje
 *    • memorija prošlih stanja
 *    • prilagođavanje ponašanja
 *
 *  Uprkos tome, ovo JE inteligentni agent prema klasičnoj definiciji.
 *
 *  OKOLINA
 *  ────────
 *  Okolina je prostorija sa promjenjivom temperaturom.
 *  Temperatura se mijenja zbog:
 *    • vanjskih uticaja
 *    • šuma senzora
 *    • simuliranih fluktuacija
 *
 *  Agent nema kontrolu nad uzrocima promjene temperature,
 *  već samo reaguje na ono što opaža.
 *
 *  GENERIC PARAMETRI AGENTA
 *  ───────────────────────
 *  TPercept    = TemperatureReading
 *      → šta agent vidi: trenutnu temperaturu (°C)
 *
 *  TAction     = ThermostatAction
 *      → šta agent može uraditi:
 *        { HeatOn, HeatOff, CoolOn, CoolOff }
 *
 *  TResult     = bool
 *      → rezultat izvršenja akcije (ovdje simboličan)
 *
 *  TExperience = bool
 *      → nema iskustva jer agent NE UČI
 *
 *  KOMPONENTE AGENTA
 *  ─────────────────
 *
 *  1) PERCEPCIJA (Sense)
 *     - TemperatureSensor
 *     Agent kontinuirano mjeri temperaturu prostorije.
 *
 *     Ovo je primjer DINAMIČKE percepcije:
 *       • vrijednost se mijenja pri svakom očitanju
 *       • agent ne dobija uvijek isti percept
 *
 *  2) POLITIKA (Think)
 *     - ThermostatPolicy
 *     Politika je skup determinističkih pravila:
 *
 *       • ako je temperatura < cilj − tolerancija → uključi grijanje
 *       • ako je temperatura > cilj + tolerancija → uključi hlađenje
 *       • inače → isključi grijanje/hlađenje
 *
 *     Politika je:
 *       • fiksna
 *       • ne adaptira se
 *       • ne zavisi od prošlih odluka
 *
 *  3) AKCIJA (Act)
 *     - ConsoleActuator<ThermostatAction>
 *     Agent izvršava odluku:
 *       • u realnom sistemu: upravljanje uređajem
 *       • ovdje: ispis u konzolu (simulacija)
 *
 *  4) UČENJE (Learn) – NE POSTOJI
 *     Ovaj agent NE UČI:
 *       • nema learner
 *       • nema feedback petlju
 *       • nema promjene politike
 *
 *  AGENTIČKI CIKLUS (Sense → Think → Act)
 *  ─────────────────────────────────────
 *  U svakoj iteraciji:
 *
 *    1) agent opaža temperaturu
 *    2) primjenjuje pravila
 *    3) izvršava akciju
 *
 *  Ovo je tzv. "simple reflex agent"
 *  prema Russell & Norvig klasifikaciji.
 *
 *  ZAŠTO JE OVO I DALJE AGENT?
 *  ──────────────────────────
 *    ✔ opaža okolinu
 *    ✔ donosi odluke
 *    ✔ utiče na okolinu
 *
 *  Agent ne mora učiti da bi bio agent.
 *
 *  ZAŠTO NIJE DOVOLJAN NA OVOM PREDMETU?
 *  ────────────────────────────────────
 *  Na ovom predmetu zahtijevamo da agent:
 *    • ima komponentu učenja
 *
 *  Ovaj primjer služi kao:
 *    • polazna tačka
 *    • kontrast prema learning agentima
 *    • referenca za razumijevanje arhitekture
 *
 *  KAKO BI POSTAO LEARNING AGENT?
 *  ─────────────────────────────
 *  Moguće nadogradnje:
 *    • adaptacija ciljne temperature
 *    • učenje tolerancije
 *    • RL agent umjesto pravila
 *
 *  KLJUČNA REČENICA ZA STUDENTE
 *  ───────────────────────────
 *  „Ovo je agent koji radi ispravno,
 *   ali nikad ne postaje pametniji."
 *
 * ───────────────────────────────────────────────────────────────────────────────
 */

        Console.WriteLine("\n┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│  1. RULE-BASED: Pametni termostat (NE UČI)                  │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");

        var sensor = new TemperatureSensor(18.0);
        var policy = new ThermostatPolicy(18.0, 1.0);
        var actuator = new ConsoleActuator<ThermostatAction>("Termostat");

        // Agent BEZ učenja - samo Sense-Think-Act
        var agent = new SoftwareAgent<TemperatureReading, ThermostatAction, bool, bool>(
            perception: sensor,
            policy: policy,
            actuator: actuator
        );

        for (int i = 0; i < 10; i++)
        {
            Console.WriteLine($"  Temp: {sensor.Observe().Celsius:F1}°C");
            agent.Step();
        }
    }
}
