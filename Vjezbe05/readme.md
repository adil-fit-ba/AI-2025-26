# SpamAgent - AI Agent Demo za Edukaciju

## Pregled

SpamAgent je edukativni projekat koji demonstrira implementaciju **inteligentnih agenata** u .NET ekosistemu. Projekat koristi klasifikaciju SMS spam poruka kao praktičan primjer za učenje koncepata:

- **Sense → Think → Act** ciklus agenta
- **Machine Learning** integracija (ML.NET)
- **Human-in-the-Loop** pattern (moderatorski review)
- **Kontinuirano učenje** (auto-retrain na osnovu gold labela)

## Arhitektura

```
┌─────────────────────────────────────────────────────────────────────┐
│                         SpamAgent System                            │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐          │
│  │   Queue      │───▶│ ScoringAgent │───▶│   Inbox/     │          │
│  │  (Queued)    │    │              │    │   Spam/      │          │
│  └──────────────┘    │ Sense:Claim  │    │   Review     │          │
│        ▲             │ Think:ML     │    └──────────────┘          │
│        │             │ Act:Decide   │           │                  │
│  ┌─────┴──────┐      └──────────────┘           │                  │
│  │ Simulator/ │                                 ▼                  │
│  │ API Input  │      ┌──────────────┐    ┌──────────────┐          │
│  └────────────┘      │ RetrainAgent │◀───│  Moderator   │          │
│                      │              │    │  (Gold Labels)│          │
│                      │ Sense:Count  │    └──────────────┘          │
│                      │ Think:Check  │                              │
│                      │ Act:Train    │                              │
│                      │ Learn:Update │                              │
│                      └──────────────┘                              │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## Projekti u Solution-u

| Projekat | Opis |
|----------|------|
| `AiAgents.Core` | Generički interfejsi i bazne klase za agente (edukativni) |
| `AiAgents.SpamAgent` | Domain, servisi, ML, produkcijski agenti |
| `AiAgents.SpamAgent.Web` | ASP.NET Core API + SignalR + Background Workers |
| `AiAgents.SpamAgent.Console` | CLI aplikacija za lokalno testiranje |
| `AiAgents.Demos` | Standalone primjeri različitih tipova agenata |

## Ključni Koncepti

### 1. Produkcijski Agenti (`Application/Agents/`)

```csharp
public interface ITickAgent<TResult> where TResult : class
{
    Task<TResult?> TickAsync(CancellationToken ct = default);
}
```

**ScoringAgent** - Procesira poruke iz queue-a:
- **Sense**: Atomični claim poruke (`Queued` → `Processing`)
- **Think**: ML model izračuna P(Spam)
- **Act**: Odluka (Allow/Block/PendingReview) + update statusa

**RetrainAgent** - Automatski retrain modela:
- **Sense**: Provjeri broj novih gold labela
- **Think**: Da li je dostignuto threshold?
- **Act**: Treniraj novi model
- **Learn**: Aktiviraj model, resetuj counter


### 2. Human-in-the-Loop

Poruke sa nesigurnom predikcijom (0.30 ≤ pSpam < 0.70) idu na moderatorski review. Moderator dodaje "gold label" koji se koristi za retrain.

### 3. Atomični Claim (Race Condition Fix)

```csharp
// QueueService.ClaimNextQueuedAsync()
var updated = await _context.Messages
    .Where(m => m.Id == candidateId && m.Status == MessageStatus.Queued)
    .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, MessageStatus.Processing), ct);

if (updated == 1)
    return await _context.Messages.FirstAsync(m => m.Id == candidateId, ct);
```

Samo jedan worker može uspješno preuzeti poruku čak i uz paralelno izvršavanje.

## Quick Start

### 1. Console App (lokalni demo)

```bash
cd AiAgents.SpamAgent.Console
dotnet run
```

Komande:
- `import` - Importuj UCI SMS dataset
- `train light` - Treniraj model (500 poruka)
- `enqueue 10` - Dodaj 10 poruka u queue
- `score` - Procesiraj queue
- `status` - Prikaži statistiku

### 2. Web App (API + Dashboard)

```bash
cd AiAgents.SpamAgent.Web
dotnet run
```

- API: `http://localhost:5000/api/`
- Swagger: `http://localhost:5000/swagger`
- SignalR Hub: `/hubs/spamagent`

### 3. Inicijalni Setup

```bash
# 1. Import dataset
curl -X POST http://localhost:5000/api/admin/import

# 2. Treniraj prvi model
curl -X POST http://localhost:5000/api/admin/train \
  -H "Content-Type: application/json" \
  -d '{"template": "Light", "activate": true}'

# 3. Uključi simulator (opciono)
curl -X POST http://localhost:5000/api/admin/simulator/true
```

## API Endpoints

### Messages
| Method | Endpoint | Opis |
|--------|----------|------|
| GET | `/api/messages` | Lista poruka |
| GET | `/api/messages/{id}` | Detalji poruke |
| POST | `/api/messages` | Dodaj novu poruku |
| GET | `/api/messages/queue` | Poruke u queue-u |
| GET | `/api/messages/pending-review` | Poruke za review |

### Review (Human-in-the-Loop)
| Method | Endpoint | Opis |
|--------|----------|------|
| GET | `/api/review/pending` | Poruke za review |
| POST | `/api/review/{id}` | Submitaj review |
| POST | `/api/review/batch` | Batch review |

### Admin
| Method | Endpoint | Opis |
|--------|----------|------|
| GET | `/api/admin/status` | System status |
| POST | `/api/admin/import` | Import dataset |
| POST | `/api/admin/train` | Treniraj model |
| POST | `/api/admin/retrain` | Force retrain |
| GET | `/api/admin/models` | Lista modela |
| PUT | `/api/admin/settings` | Ažuriraj postavke |

## SignalR Events

```javascript
connection.on("MessageQueued", (evt) => { /* nova poruka u queue */ });
connection.on("MessageScored", (evt) => { /* poruka procesirana */ });
connection.on("ModelRetrained", (evt) => { /* novi model */ });
connection.on("StatsUpdated", (evt) => { /* ažurirana statistika */ });
```

## Konfiguracija

### appsettings.json

```json
{
  "ConnectionStrings": {
    "SpamAgent": "Data Source=spamagent.db"
  },
  "SpamAgent": {
    "DatasetPath": "Dataset/SMSSpamCollection",
    "ModelsDirectory": "Models"
  },
  "Simulator": {
    "Enabled": false,
    "IntervalMs": 3000,
    "BatchSize": 1
  }
}
```

### Pragovi Odlučivanja

| Prag | Default | Opis |
|------|---------|------|
| ThresholdAllow | 0.30 | pSpam < 0.30 → Inbox |
| ThresholdBlock | 0.70 | pSpam ≥ 0.70 → Spam |
| (između) | - | PendingReview |

## Folder Struktura

```
AiAgents.SpamAgent/
├── Abstractions/
│   └── ITickAgent.cs           # Produkcijski interfejsi
├── Application/
│   ├── ProductionAgents/       # Produkcijski agenti
│   │   ├── ScoringAgent.cs
│   │   └── RetrainAgent.cs
│   ├── Services/               # Business logika
│   └── Queries/                # Read-only queries
├── Domain/                     # Entiteti i enumi
├── Infrastructure/             # DbContext, Seeder
└── ML/                         # ML.NET klasifikator
```

## MessageStatus Flow

```
Dataset ──(enqueue)──▶ Queued ──(claim)──▶ Processing ──(score)──┬──▶ InInbox
                                                                 ├──▶ InSpam
                                                                 └──▶ PendingReview ──(review)──▶ InInbox/InSpam
```

## Tehnologije

- .NET 8.0
- ASP.NET Core (Web API, SignalR)
- Entity Framework Core 8.0 (SQLite)
- ML.NET 3.0 (SDCA Logistic Regression)

## Dataset

UCI SMS Spam Collection - 5,574 SMS poruka (ham/spam).

- **Train Pool**: 80% (4,459 poruka)
- **Validation Holdout**: 20% (1,115 poruka)

## Licenca

MIT - Slobodno za edukativnu upotrebu.