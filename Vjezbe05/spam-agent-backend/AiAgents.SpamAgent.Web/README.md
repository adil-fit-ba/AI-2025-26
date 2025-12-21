# Spam Agent Web API

Web API + SignalR za SMS Spam klasifikaciju s inteligentnim agentom.

## Arhitektura

```
┌─────────────────────────────────────────────────────────────────────┐
│                         SPAM AGENT SYSTEM                          │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│   ┌─────────────┐     ┌─────────────┐     ┌─────────────┐          │
│   │   INCOMING  │────▶│   SCORING   │────▶│   SORTED    │          │
│   │   QUEUE     │     │   RUNNER    │     │  (Inbox/    │          │
│   │             │     │             │     │  Spam/      │          │
│   │  Status:    │     │ Sense→Think │     │  Pending)   │          │
│   │  Queued     │     │ →Act        │     │             │          │
│   └─────────────┘     └─────────────┘     └─────────────┘          │
│                              │                    │                 │
│                              │              ┌─────▼─────┐           │
│                              │              │ MODERATOR │           │
│                              │              │  REVIEW   │           │
│                              │              │  (Gold)   │           │
│                              │              └─────┬─────┘           │
│                              │                    │                 │
│                        ┌─────▼─────────────────────▼─────┐          │
│                        │        RETRAIN RUNNER          │          │
│                        │  Sense→Think→Act→Learn         │          │
│                        │  (auto-retrain kad gold >= N)  │          │
│                        └────────────────────────────────┘          │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## Struktura projekta (Layered Architecture)

```
AiAgents.SpamAgent/                    # Shared Library
├── Domain/
│   ├── Entities.cs                    # Message, Prediction, Review, ModelVersion
│   └── Enums.cs                       # Label, MessageStatus, SpamDecision
├── Application/
│   ├── Services/                      # Command/Use-case servisi
│   │   ├── QueueService.cs
│   │   ├── ReviewService.cs
│   │   ├── ScoringService.cs
│   │   └── TrainingService.cs
│   ├── Queries/                       # Read-only query servisi
│   │   ├── MessageQueryService.cs
│   │   └── AdminQueryService.cs
│   └── Runners/                       # Agent loop logika
│       ├── ScoringAgentRunner.cs      # Sense→Think→Act
│       └── RetrainAgentRunner.cs      # Sense→Think→Act→Learn
├── Infrastructure/
│   ├── SpamAgentDbContext.cs
│   └── DatabaseSeeder.cs
├── ML/
│   ├── ISpamClassifier.cs
│   └── MlNetSpamClassifier.cs
├── ServiceCollectionExtensions.cs     # DI registration
└── SpamAgentOptions.cs                # Configuration

AiAgents.SpamAgent.Web/                # Web Host (tanki sloj)
├── Controllers/                       # Validacija + poziv servisa + emit event
│   ├── MessagesController.cs
│   ├── ReviewController.cs
│   └── AdminController.cs
├── BackgroundServices/                # Scope-per-iteration wrappers
│   ├── ScoringWorkerService.cs
│   ├── RetrainWorkerService.cs
│   └── SimulatorService.cs
├── Hubs/
│   └── SpamAgentHub.cs
├── Models/
│   └── ApiModels.cs                   # DTOs za API
└── Program.cs                         # AddSpamAgentServices() + hosting
```

## Quick Start

```bash
cd src/AiAgents.SpamAgent.Web
dotnet run
```

Swagger UI: http://localhost:5000

## API Endpoints

### Messages (Public)
| Method | Endpoint | Opis |
|--------|----------|------|
| POST | `/api/messages` | Pošalji poruku u queue |
| GET | `/api/messages/{id}` | Dohvati poruku sa predikcijom |
| GET | `/api/messages/recent` | Nedavno procesirane poruke |
| GET | `/api/messages/queued` | Poruke u queue-u |
| POST | `/api/messages/enqueue?count=10` | Dodaj iz validation seta (demo) |
| GET | `/api/messages/stats` | Statistika po statusima |

### Review (Moderation)
| Method | Endpoint | Opis |
|--------|----------|------|
| GET | `/api/review/queue` | Poruke koje čekaju review |
| GET | `/api/review/count` | Broj pending poruka |
| POST | `/api/review/{messageId}` | Dodaj gold label |
| GET | `/api/review/stats` | Statistika gold labela |

### Admin
| Method | Endpoint | Opis |
|--------|----------|------|
| GET | `/api/admin/status` | Kompletni status sistema |
| POST | `/api/admin/import` | Importuj UCI dataset |
| POST | `/api/admin/train` | Treniraj novi model |
| POST | `/api/admin/retrain` | Forsiraj retrain |
| GET | `/api/admin/models` | Sve verzije modela |
| POST | `/api/admin/models/{v}/activate` | Aktiviraj model |
| GET | `/api/admin/settings` | Dohvati postavke |
| PUT | `/api/admin/settings` | Ažuriraj postavke |
| GET | `/api/admin/simulator` | Status simulatora |
| POST | `/api/admin/simulator/{enabled}` | Uključi/isključi simulator |

## SignalR Hub

**URL:** `http://localhost:5000/hubs/spamAgent`

### Eventi

| Event | Grupa | Opis |
|-------|-------|------|
| `MessageQueued` | messages | Nova poruka u queue-u |
| `MessageScored` | messages | Poruka procesirana |
| `MessageMoved` | messages | Poruka premještena (review) |
| `ModelRetrained` | models | Novi model treniran |
| `ModelActivated` | models | Model aktiviran |
| `StatsUpdated` | stats | Statistika ažurirana |

### JavaScript Client Primjer

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5000/hubs/spamAgent")
    .build();

connection.on("MessageScored", (evt) => {
    console.log(`Message ${evt.messageId}: pSpam=${evt.pSpam}, Decision=${evt.decision}`);
});

connection.on("ModelRetrained", (evt) => {
    console.log(`New model v${evt.newVersion}, Accuracy=${evt.metrics.accuracy}`);
});

await connection.start();
```

## Demo Scenarij

1. **Import dataset:**
   ```
   POST /api/admin/import
   ```

2. **Treniraj model:**
   ```
   POST /api/admin/train
   Body: { "template": "Medium", "activate": true }
   ```

3. **Dodaj poruke u queue:**
   ```
   POST /api/messages/enqueue?count=30
   ```

4. **Gledaj rezultate:**
   ```
   GET /api/messages/recent
   ```
   Ili prati SignalR evente.

5. **Review pending poruka:**
   ```
   GET /api/review/queue
   POST /api/review/{id}
   Body: { "label": "spam" }
   ```

6. **Kad se nakupi dovoljno gold labela → auto-retrain!**

## Simulator

Simulator automatski dodaje poruke u queue (za demo bez FE).

Uključi:
```
POST /api/admin/simulator/true
```

Ili u `appsettings.json`:
```json
{
  "Simulator": {
    "Enabled": true,
    "IntervalMs": 3000,
    "BatchSize": 1
  }
}
```

## Background Servisi

| Servis | Opis |
|--------|------|
| `AgentWorkerService` | Procesira queue (Sense→Think→Act) |
| `RetrainWorkerService` | Automatski retrain kad gold >= threshold |
| `SimulatorService` | Generira poruke za demo (opciono) |

## Napomene

⚠️ **Admin endpointi nemaju autentikaciju** - nisu za produkciju!

Za produkciju dodati:
- JWT ili API Key autentikaciju
- Rate limiting
- HTTPS
