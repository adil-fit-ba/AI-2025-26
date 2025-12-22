# Softverski Inteligentni Agenti

> **Edukativni projekat koji demonstrira univerzalnu arhitekturu inteligentnih agenata kroz različite tipove implementacija**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-12.0-239120?logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/badge/License-Educational-blue.svg)]()

---

## 📖 Sadržaj

- [O Projektu](#-o-projektu)
- [Ključni Koncepti](#-ključni-koncepti)
- [Arhitektura](#-arhitektura)
- [Primjeri Agenata](#-primjeri-agenata)
- [Instalacija i Pokretanje](#-instalacija-i-pokretanje)
- [Struktura Projekta](#-struktura-projekta)
- [Pedagoški Ciljevi](#-pedagoški-ciljevi)
- [Tehnički Detalji](#-tehnički-detalji)

---

## 🎯 O Projektu

Ovaj projekat demonstrira **UNIVERZALNU arhitekturu softverskih inteligentnih agenata** kroz implementaciju šest različitih tipova agenata koji svi dijele istu osnovnu strukturu.

### Motivacija

U AI literaturi postoji mnogo različitih pristupa implementaciji inteligentnih agenata. Ovaj projekat pokazuje da **svi agenti, bez obzira na tip, dijele istu fundamentalnu arhitekturu**:

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│  PERCEPCIJA │ ──► │   POLITIKA  │ ──► │   AKTUATOR  │
│   (Sense)   │     │   (Think)   │     │    (Act)    │
└─────────────┘     └─────────────┘     └─────────────┘
       │                   ▲                   │
       │                   │                   │
       │            ┌──────┴──────┐            │
       └──────────► │   UČENJE    │ ◄──────────┘
                    │  (Learn)    │
                    └─────────────┘
```

### Ključna Ideja

**Razlika između tipova agenata je SAMO u implementaciji komponenti, ne u arhitekturi!**

- Rule-Based agent → IF-THEN pravila
- Supervised Learning agent → ML model
- Reinforcement Learning agent → Q-Learning
- Human-in-the-Loop agent → feedback od korisnika
- LLM-Powered agent → Large Language Model
- Q-Learning Vacuum Cleaner → kompletan RL primjer sa vizualizacijom

---

## 🔑 Ključni Koncepti

### Inteligentni Agent

Softverski sistem koji:
- ✅ **Opaža** okolinu (percepcija)
- ✅ **Donosi odluke** (politika)
- ✅ **Utiče** na okolinu (akcija)
- ✅ **Uči** iz iskustva (učenje)

### Četiri Osnovna Interfejsa

#### 1. `IPerceptionSource<TPercept>`
```csharp
public interface IPerceptionSource<TPercept>
{
    TPercept Observe();
}
```
**Odgovornost:** Omogućava agentu da "vidi" okolinu.

**Primjeri:**
- Senzor temperature
- Email inbox
- Stanje grid mreže
- Ticket queue

#### 2. `IPolicy<TPercept, TAction>`
```csharp
public interface IPolicy<TPercept, TAction>
{
    TAction SelectAction(TPercept percept);
}
```
**Odgovornost:** "Mozak" agenta - odlučuje šta uraditi.

**Primjeri:**
- IF-THEN pravila
- Q-tabela
- ML model
- LLM prompt

#### 3. `IActuator<TAction, TResult>`
```csharp
public interface IActuator<TAction, TResult>
{
    TResult Execute(TAction action);
}
```
**Odgovornost:** Izvršava akciju i vraća rezultat.

**Primjeri:**
- Upravljanje uređajem
- Pomjeranje robota
- Slanje odgovora korisniku
- Čišćenje tile-a

#### 4. `ILearningComponent<TExperience>` (opciono)
```csharp
public interface ILearningComponent<TExperience>
{
    void Learn(TExperience experience);
}
```
**Odgovornost:** Poboljšava ponašanje agenta kroz vrijeme.

**Primjeri:**
- Q-Learning algoritam
- Ažuriranje modela
- Prilagođavanje parametara
- Učenje iz feedbacka

---

## 🏗️ Arhitektura

### Generički Agent

Svaki agent je instanca `SoftwareAgent<TPercept, TAction, TResult, TExperience>`:

```csharp
public class SoftwareAgent<TPercept, TAction, TResult, TExperience> : IAgent
{
    public virtual void Step()
    {
        // 1. SENSE  → Prikupi informacije iz okoline
        var percept = _perception.Observe();

        // 2. THINK  → Odluči šta uraditi
        var action = _policy.SelectAction(percept);

        // 3. ACT    → Izvrši akciju, dobij rezultat
        var result = _actuator.Execute(action);

        // 4. LEARN  → Poboljšaj se na osnovu iskustva
        if (_learner != null && _experienceBuilder != null)
        {
            var experience = _experienceBuilder(percept, action, result);
            _learner.Learn(experience);
        }
    }
}
```

### Sense → Think → Act → Learn Ciklus

Svaki poziv `agent.Step()` prolazi kroz četiri faze:

1. **SENSE** - Agent opaža trenutno stanje okoline
2. **THINK** - Agent odlučuje koju akciju preduzeti
3. **ACT** - Agent izvršava akciju i dobija odgovor od okoline
4. **LEARN** - Agent koristi iskustvo da poboljša buduće odluke

---

## 🤖 Primjeri Agenata

### A. Rule-Based Agent (Termostat)

**Tip:** Reaktivni agent bez učenja  
**Politika:** IF-THEN pravila  
**Cilj:** Održavanje temperature

```
📊 Komponente:
  • TPercept    = TemperatureReading (temperatura u °C)
  • TAction     = ThermostatAction {HeatOn, HeatOff, CoolOn, CoolOff}
  • TResult     = bool (uspješnost izvršenja)
  • TExperience = N/A (ne uči)

💡 Karakteristike:
  ✓ Deterministička pravila
  ✓ Nema učenje
  ✓ Brzo i pouzdano
  ✗ Ne prilagođava se
```

**Učenje:** Nema - pravila su fiksna

---

### B. Supervised Learning Agent (Spam Detektor)

**Tip:** Agent sa nadgledanim učenjem  
**Politika:** Naučeni ML model  
**Cilj:** Klasifikacija email poruka

```
📊 Komponente:
  • TPercept    = EmailFeatures (subject, links, suspicious words)
  • TAction     = EmailClass {NotSpam, Spam}
  • TResult     = EmailClass (tačna labela)
  • TExperience = SupervisedExperience (predicted vs actual)

💡 Karakteristike:
  ✓ Uči iz labeliranih primjera
  ✓ DynamicPerception (inbox queue)
  ✓ Kontinuirano procesiranje
  ✓ Prilagođava se novim podacima
```

**Učenje:** Ažurira model na osnovu razlike između predviđene i stvarne labele

**Ključna Tehnika:** `DynamicPerception` omogućava agentu da procesira email-ove u toku (stream), ne kao batch.

---

### C. Reinforcement Learning Agent (Robot)

**Tip:** RL agent sa Q-Learning algoritmom  
**Politika:** Q-tabela sa epsilon-greedy strategijom  
**Cilj:** Stići do pozicije 5 na traci

```
📊 Komponente:
  • TPercept    = RobotState (pozicija [0..5])
  • TAction     = RobotAction {Left, Right}
  • TResult     = RLStepResult (novo stanje, nagrada)
  • TExperience = RLExperience (state, action, reward, nextState)

💡 Karakteristike:
  ✓ Uči iz nagrada
  ✓ Epsilon-greedy eksploracija
  ✓ Goal-oriented ponašanje
  ✓ Q-Learning: Q(s,a) ← Q(s,a) + α[r + γ max Q(s',a') − Q(s,a)]
```

**Učenje:** Q-Learning algoritam ažurira vrijednosti stanja-akcija parova

**Q-Learning Formula:**
```
Q(s,a) ← Q(s,a) + α [ r + γ max Q(s',a') − Q(s,a) ]

gdje:
  • α (alpha) = stopa učenja (learning rate)
  • γ (gamma) = faktor diskontiranja (discount factor)
  • r         = nagrada (reward)
```

---

### D. Human-in-the-Loop Agent (Preporuka filmova)

**Tip:** Agent koji uči iz ljudskog feedbacka  
**Politika:** Model koji se prilagođava preferencama  
**Cilj:** Davanje personalizovanih preporuka

```
📊 Komponente:
  • TPercept    = UserQuery (žanr, mood)
  • TAction     = MovieRecommendation
  • TResult     = UserFeedback (ocjena 1-5)
  • TExperience = FeedbackExperience (akcija + ocjena)

💡 Karakteristike:
  ✓ Učenje iz ljudskog feedbacka
  ✓ DynamicPerception (ticket queue)
  ✓ Personalizacija kroz vrijeme
  ✓ Interaktivno poboljšanje
```

**Učenje:** Povećava vjerovatnoću uspješnih preporuka na osnovu ocjena korisnika

---

### E. LLM-Powered Agent (Korisnička podrška)

**Tip:** Agent zasnovan na jezičkom modelu  
**Politika:** Large Language Model (simuliran)  
**Cilj:** Rješavanje korisničkih upita

```
📊 Komponente:
  • TPercept    = SupportTicket (tekst upita)
  • TAction     = SupportResponse (odgovor, tip)
  • TResult     = SupervisorDecision (prihvati/odbaci/eskalacija)
  • TExperience = SupervisedExperience (response + decision)

💡 Karakteristike:
  ✓ Kontekstualno razumijevanje
  ✓ Generisanje prirodnog jezika
  ✓ Supervizija i eskalacija
  ✓ DynamicPerception (ticket queue)
  ✓ Simulacija LLM-a (demonstracijski primjer)
```

**Učenje:** Učenje iz supervizorskih odluka (prihvatanje/odbijanje odgovora)

**Arhitektura:**
- **Ticket Queue** - Simulirani stream korisničkih upita
- **LLM Policy** - Generisanje odgovora
- **Supervisor** - Kontrola kvaliteta
- **Learner** - Prilagođavanje na osnovu feedbacka

---

### F. Q-Learning Vacuum Cleaner

**Tip:** Kompletan RL agent sa vizualizacijom  
**Politika:** Q-Learning na grid mreži  
**Cilj:** Očistiti sve prljave pločice u gridu

```
📊 Komponente:
  • TPercept    = VacuumState (pozicija, status grid-a)
  • TAction     = VacuumAction {Up, Down, Left, Right, Suck}
  • TResult     = VacuumStepResult (novo stanje, nagrada)
  • TExperience = VacuumExperience (SARSA tuple)

💡 Karakteristike:
  ✓ 2D grid world
  ✓ Kompleksna prostorna navigacija
  ✓ 5 akcija (4 smjera + Suck)
  ✓ Vizualizacija grid-a i Q-vrijednosti
  ✓ Nasljeđuje SoftwareAgent (pokazuje ekstenzibilnost)
```

**Grid Okolina:**
```
┌───┬───┬───┬───┬───┐
│ A │ ∙ │ ∙ │ ∙ │ ∙ │
├───┼───┼───┼───┼───┤
│ ∙ │ ∙ │ ∙ │ ∙ │ ∙ │
├───┼───┼───┼───┼───┤
│ ∙ │ ∙ │ ∙ │ ∙ │ ∙ │
├───┼───┼───┼───┼───┤
│ ∙ │ ∙ │ ∙ │ ∙ │ ∙ │
└───┴───┴───┴───┴───┘

Legenda:
  A = Agent (Vacuum Cleaner)
  ∙ = Clean (čisto)
  ░ = Dirty (prljavo)
```

**Učenje:** Q-Learning na svim kombinacijama stanja (pozicija + status pločica)

**Specifičnosti:**
- Nasljeđuje `SoftwareAgent` - demonstrira kako se osnovna arhitektura može ekstendovati
- Prilagođena `Step()` metoda sa vizualizacijom
- Dodati metodi za prikaz grid-a i Q-vrijednosti

---

## 🚀 Instalacija i Pokretanje

### Preduvjeti

- **.NET 8.0 SDK** ili noviji
- IDE: Visual Studio, Rider, ili VS Code sa C# ekstenzijom

### Kloniranje Projekta

```bash
git clone <repository-url>
cd AI-Agents
```

### Kompajliranje

```bash
dotnet build
```

### Pokretanje

```bash
dotnet run
```

### Interaktivni Meni

Nakon pokretanja, vidjet ćete meni sa opcijama:

```
+===============================================================+
|     DEMONSTRACIJA TIPOVA SOFTVERSKIH AGENATA                  |
+===============================================================+
|  Svi koriste ISTE INTERFEJSE, razlicite implementacije!       |
+===============================================================+

  Odaberite primjer:

  [A] PRIMJER A: Rule-Based Agent (Termostat)
  [B] PRIMJER B: Supervised Learning Agent (Spam Detektor)
  [C] PRIMJER C: Reinforcement Learning Agent (Robot)
  [D] PRIMJER D: Human-in-the-Loop Agent (Preporuka filmova)
  [E] PRIMJER E: LLM-Powered Agent (Korisnička podrška)
  [F] PRIMJER F: Q-Learning Vacuum Cleaner
  [7] Pokreni SVE primjere redom (osim Vacuum Cleaner-a)
  [Q] Izlaz

  Vaš izbor:
```

---

## 📁 Struktura Projekta

```
AI-Agents/
│
├── SharedCore.cs                           # Osnovna arhitektura
│   ├── IPerceptionSource<T>                # Interface za percepciju
│   ├── IPolicy<TPercept, TAction>          # Interface za politiku
│   ├── IActuator<TAction, TResult>         # Interface za aktuator
│   ├── ILearningComponent<TExperience>     # Interface za učenje
│   ├── IAgent                              # Interface za agenta
│   ├── SoftwareAgent<...>                  # Generička implementacija
│   ├── StaticPerception<T>                 # Fiksna percepcija
│   ├── DynamicPerception<T>                # Dinamička percepcija
│   └── ConsoleActuator<T>                  # Jednostavna akcija
│
├── ExampleA_RuleBasedAgent.cs              # Termostat (IF-THEN pravila)
│   ├── TemperatureReading                  # Percepcija temperature
│   ├── ThermostatAction                    # Akcije termostata
│   ├── ThermostatPolicy                    # Pravila odlučivanja
│   └── TemperatureSensor                   # Simulirani senzor
│
├── ExampleB_SupervisedLearningAgent.cs     # Spam detektor
│   ├── EmailFeatures                       # Feature reprezentacija
│   ├── EmailClass                          # Spam/NotSpam
│   ├── SpamClassifierPolicy                # ML model
│   ├── DynamicSpamOracle                   # Oracle za labele
│   └── SupervisedLearner                   # Učenje iz labela
│
├── ExampleC_ReinforcementLearningAgent.cs  # Robot na traci
│   ├── RobotState                          # Pozicija robota
│   ├── RobotAction                         # Left/Right
│   ├── RobotEnvironment                    # 1D traka
│   ├── SimpleQPolicy                       # Q-tabela + epsilon-greedy
│   └── QLearner                            # Q-Learning algoritam
│
├── ExampleD_HumanInLoopAgent.cs            # Preporuka filmova
│   ├── UserQuery                           # Upit korisnika
│   ├── MovieRecommendation                 # Preporuka filma
│   ├── MovieRecommenderPolicy              # Model preporuka
│   ├── UserFeedbackOracle                  # Ocjene korisnika
│   └── FeedbackLearner                     # Učenje iz ocjena
│
├── ExampleE_LLMPoweredAgent.cs             # Korisnička podrška
│   ├── SupportTicket                       # Ticket sa upitom
│   ├── SupportResponse                     # Odgovor agenta
│   ├── LLMPolicy                           # Simulirani LLM
│   ├── QualitySupervisor                   # Supervizor kvaliteta
│   └── SupervisedLearner                   # Učenje iz supervizije
│
├── ExampleF_VacuumCleaner.cs               # Q-Learning Vacuum
│   ├── VacuumState                         # Pozicija + grid status
│   ├── VacuumAction                        # Up/Down/Left/Right/Suck
│   ├── VacuumEnvironment                   # 2D grid world
│   ├── VacuumQPolicy                       # Q-Learning politika
│   ├── VacuumQLearner                      # Q-Learning updater
│   └── VacuumAgent : SoftwareAgent         # Ekstenzija osnovnog agenta
│
├── Program.cs                              # Glavni program + meni
│
└── README.md                               # Dokumentacija (ovaj fajl)
```

---

## 🎓 Pedagoški Ciljevi

### 1. Univerzalnost Arhitekture

**Cilj:** Studenti shvataju da svi inteligentni agenti dijele istu strukturu.

**Metoda:** Šest različitih tipova agenata, svi koriste iste interfejse.

**Ishod:** Razumijevanje da razlika nije u arhitekturi, već u implementaciji komponenti.

---

### 2. Razlika: Agent vs. Algoritam

**Problem:** Studenti često brka ML algoritme sa agentima.

**Rješenje:** 
- **Algoritam** = funkcija koja se pozove jednom
- **Agent** = proces koji kontinuirano opaža, odlučuje i uči

**Primjer:** Spam detektor sa `DynamicPerception` - agent procesira stream emailova, ne batch dataset.

---

### 3. Percepcija: Static vs. Dynamic

**Static Perception:**
```csharp
var perception = new StaticPerception<int>(42);
// Uvijek vraća 42
```

**Dynamic Perception:**
```csharp
var queue = new Queue<Email>();
var perception = new DynamicPerception<Email>(() => queue.Dequeue());
// Svaki put vraća NOVI email iz queue-a
```

**Cilj:** Razumijevanje da agenti rade sa PROMJENLJIVOM okolinom.

---

### 4. Shared State Pattern

**Problem:** Kako percepcija i oracle dijele istu informaciju?

**Primjer iz Spam Detektora:**
```csharp
(EmailFeatures email, EmailClass label) currentEmail = default;

var perception = new DynamicPerception<EmailFeatures>(() => {
    currentEmail = inboxQueue.Dequeue();
    return currentEmail.email;
});

var oracle = new DynamicSpamOracle(() => currentEmail.label);
```

**Cilj:** Razumijevanje sinhronizacije između komponenti agenta.

---

### 5. Goal-Oriented vs. Continuous Agents

**Goal-Oriented:**
- Robot (stići do pozicije 5)
- Vacuum Cleaner (očistiti sve pločice)

```csharp
for (int step = 0; step < maxSteps && !agent.IsGoalReached; step++)
{
    agent.Step();
}
```

**Continuous:**
- Termostat (radi beskonačno)
- Spam detektor (procesira sve emailove)

```csharp
while (true)
{
    agent.Step();
}
```

---

### 6. Ekstenzibilnost: Nasljeđivanje

**Primjer:** `VacuumAgent` nasljeđuje `SoftwareAgent`

```csharp
public sealed class VacuumAgent : SoftwareAgent<VacuumState, VacuumAction, VacuumStepResult, VacuumExperience>
{
    public override void Step()
    {
        base.Step();           // Poziva originalni ciklus
        RenderGrid();          // Dodaje vizualizaciju
    }
}
```

**Cilj:** Pokazuje kako se osnovna arhitektura može prilagoditi bez mijenjanja core logike.

---

## 🔧 Tehnički Detalji

### Generic Type Parameters

Svaki agent je parametrizovan sa četiri tipa:

```csharp
SoftwareAgent<TPercept, TAction, TResult, TExperience>
```

| Parametar | Opis | Primjer |
|-----------|------|---------|
| `TPercept` | Šta agent vidi | `TemperatureReading`, `EmailFeatures` |
| `TAction` | Šta agent može uraditi | `ThermostatAction`, `RobotAction` |
| `TResult` | Šta okolina vraća | `bool`, `RLStepResult` |
| `TExperience` | Iskustvo za učenje | `RLExperience`, `SupervisedExperience` |

---

### Dependency Injection Pattern

Agenti koriste constructor injection:

```csharp
var agent = new SoftwareAgent<...>(
    perception: senzor,
    policy: mozak,
    actuator: izvršilac,
    experienceBuilder: eksperiencija,
    learner: učenje,
    goalChecker: cilj
);
```

**Prednosti:**
- ✅ Testabilnost (lako mock-ovati komponente)
- ✅ Fleksibilnost (zamjena implementacija)
- ✅ Modularnost (nezavisne komponente)

---

### Optional Components

Učenje i goal checker su **opcioni**:

```csharp
// Agent BEZ učenja (Rule-Based)
var agent = new SoftwareAgent<...>(
    perception: sensor,
    policy: policy,
    actuator: actuator
    // learner: null (implicitno)
    // goalChecker: null (implicitno)
);

// Agent SA učenjem (RL)
var agent = new SoftwareAgent<...>(
    perception: env,
    policy: qPolicy,
    actuator: env,
    experienceBuilder: (s, a, r) => new Experience(s, a, r),
    learner: qLearner,
    goalChecker: () => env.IsAtGoal
);
```

---

### Record Structs

Projekat koristi `readonly record struct` za podatke:

```csharp
public readonly record struct RobotState(int Position);
```

**Prednosti:**
- ✅ Value semantics (kopiranje po vrijednosti)
- ✅ Immutability (ne može se mijenjati)
- ✅ Automatic equality (strukturna jednakost)
- ✅ Performance (stack allocation)

---

### Enum za Akcije

Korištenje enumeracija umjesto integer konstanti:

```csharp
public enum RobotAction { Left, Right }
```

**Prednosti:**
- ✅ Type safety (kompajler provjerava)
- ✅ Čitljivost (samoobjašnjavajuće)
- ✅ IntelliSense podrška

---

## 📊 Poređenje Agenata

| Agent | Politika | Učenje | Percepcija | Goal | Složenost |
|-------|----------|--------|------------|------|-----------|
| **Termostat** | IF-THEN | ❌ | Dynamic | ❌ | ⭐ |
| **Spam Detektor** | ML Model | ✅ | Dynamic Queue | ❌ | ⭐⭐ |
| **Robot** | Q-Learning | ✅ | Static | ✅ | ⭐⭐ |
| **Movie Recommender** | Feedback Model | ✅ | Dynamic Queue | ❌ | ⭐⭐⭐ |
| **Customer Support** | LLM (sim.) | ✅ | Dynamic Queue | ❌ | ⭐⭐⭐⭐ |
| **Vacuum Cleaner** | Q-Learning Grid | ✅ | 2D Grid | ✅ | ⭐⭐⭐⭐⭐ |

---

## 🎯 Ključne Lekcije

### 1. **Arhitektura > Implementacija**
Svi agenti koriste istu arhitekturu. Razlika je samo u tome **kako** su komponente implementirane.

### 2. **Agent ≠ Algoritam**
Agent je proces koji kontinuirano opaža i reaguje. Algoritam je funkcija koja se pozove jednom.

### 3. **Učenje je Opciono**
Ne svi agenti moraju učiti. Rule-based agenti mogu biti vrlo korisni bez učenja.

### 4. **Percepcija je Dinamička**
U realnim sistemima, percepcija se mijenja. `DynamicPerception` je ključna za modelovanje takvih sistema.

### 5. **Shared State je Realan Pattern**
Komponente agenta često dijele istu informaciju o okolini. Ovo nije bug - to je feature.

### 6. **Ekstenzibilnost kroz Nasljeđivanje**
Osnovna arhitektura se može ekstendovati (Vacuum Cleaner primjer) bez mijenjanja core logike.

---

## 🔮 Dalja Proširenja

Mogući pravci za proširenje projekta:

### 1. Multi-Agent Sistemi
```csharp
public class MultiAgentEnvironment<TState>
{
    private List<IAgent> _agents;
    
    public void Step()
    {
        foreach (var agent in _agents)
        {
            agent.Step();
        }
    }
}
```

### 2. Asinkroni Agenti
```csharp
public interface IAsyncAgent
{
    Task StepAsync();
}
```

### 3. Neural Network Policies
```csharp
public class NeuralNetworkPolicy : IPolicy<Vector, int>
{
    private readonly IModel _model;
    
    public int SelectAction(Vector state)
    {
        return _model.Predict(state);
    }
}
```

### 4. Komunikacija Između Agenata
```csharp
public interface IMessageBroker<TMessage>
{
    void Send(TMessage message);
    TMessage? Receive();
}
```
