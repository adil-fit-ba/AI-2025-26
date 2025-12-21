# Spam Agent Frontend

Angular 17 frontend aplikacija za SMS Spam klasifikaciju sa real-time SignalR komunikacijom.

## Arhitektura

```
┌─────────────────────────────────────────────────────────────────────┐
│                         FRONTEND APP                                │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│   ┌────────────────┐   ┌────────────────┐   ┌────────────────┐     │
│   │   Dashboard    │   │    Review      │   │     Admin      │     │
│   │   (Live Demo)  │   │  (Moderation)  │   │   (Settings)   │     │
│   └───────┬────────┘   └───────┬────────┘   └───────┬────────┘     │
│           │                    │                    │               │
│           └────────────────────┼────────────────────┘               │
│                                │                                    │
│                    ┌───────────▼───────────┐                        │
│                    │    State Service      │                        │
│                    │  (RxJS BehaviorSubject)│                        │
│                    └───────────┬───────────┘                        │
│                                │                                    │
│           ┌────────────────────┼────────────────────┐               │
│           │                    │                    │               │
│   ┌───────▼────────┐   ┌───────▼────────┐   ┌───────▼────────┐     │
│   │   API Service  │   │ SignalR Service │   │  (Future...)  │     │
│   │   (HTTP REST)  │   │  (Real-time)    │   │               │     │
│   └───────┬────────┘   └───────┬────────┘   └────────────────┘     │
│           │                    │                                    │
└───────────┼────────────────────┼────────────────────────────────────┘
            │                    │
            ▼                    ▼
    ┌───────────────────────────────────────┐
    │         Backend API (C# Web API)      │
    │         http://localhost:5000         │
    └───────────────────────────────────────┘
```

## Ekrani

### 1. Dashboard (Live Demo)
- **4 kolone**: Incoming → Inbox / Spam / Review
- **Real-time animacije** - poruke se animirano premještaju
- **Stats panel** - aktivni model, metrike, gold progress
- **Enqueue button** - dodaj poruke iz validation seta

### 2. Review (Moderation)
- **Lista pending poruka** - poruke koje čekaju review
- **Detail panel** - prikaz poruke, predikcije, ground truth
- **HAM/SPAM dugmad** - označavanje gold labela
- **Gold progress** - progres do auto-retrain-a

### 3. Admin
- **Quick actions** - import, enqueue, simulator, force retrain
- **Training panel** - odabir template-a (Light/Medium/Full)
- **Settings** - pragovi, auto-retrain threshold
- **Models table** - sve verzije modela sa metrikama

## Tehnologije

- **Angular 17** - standalone komponente
- **Tailwind CSS** - utility-first styling
- **RxJS** - reactive state management
- **@microsoft/signalr** - real-time komunikacija

## Instalacija

```bash
cd spam-agent-frontend
npm install
```

## Pokretanje

```bash
npm start
# ili
ng serve
```

Aplikacija će biti dostupna na: http://localhost:4200

**Napomena:** Backend mora biti pokrenut na http://localhost:5000

## Struktura projekta

```
src/
├── app/
│   ├── components/           # Reusable komponente
│   │   ├── message-card/
│   │   └── stats-panel/
│   ├── pages/                # Page komponente
│   │   ├── dashboard/
│   │   ├── review/
│   │   └── admin/
│   ├── services/             # Servisi
│   │   ├── api.service.ts    # HTTP REST API
│   │   ├── signalr.service.ts# Real-time events
│   │   └── state.service.ts  # State management
│   ├── models/               # TypeScript modeli
│   │   └── api.models.ts
│   ├── app.component.ts      # Root komponenta
│   ├── app.routes.ts         # Routing
│   └── app.config.ts         # App config
├── styles.css                # Global Tailwind styles
└── index.html
```

## SignalR Events

Frontend sluša sljedeće SignalR evente:

| Event | Opis |
|-------|------|
| `MessageQueued` | Nova poruka dodana u queue |
| `MessageScored` | Poruka procesirana - pSpam, decision, status |
| `MessageMoved` | Poruka premještena (review) |
| `ModelRetrained` | Novi model treniran |
| `StatsUpdated` | Statistika ažurirana |

## API Endpoints

Frontend koristi sljedeće API endpointe:

### Messages
- `POST /api/messages` - pošalji poruku
- `GET /api/messages/recent` - nedavne poruke
- `POST /api/messages/enqueue` - dodaj iz validation seta

### Review
- `GET /api/review/queue` - pending poruke
- `POST /api/review/{id}` - dodaj gold label

### Admin
- `GET /api/admin/status` - status sistema
- `POST /api/admin/train` - treniraj model
- `GET /api/admin/models` - sve verzije modela
- `PUT /api/admin/settings` - ažuriraj postavke

## Demo Flow

1. **Pokreni backend** na http://localhost:5000
2. **Pokreni frontend** na http://localhost:4200
3. **Admin → Import Dataset** - učitaj UCI SMS data
4. **Admin → Train Model** - treniraj prvi model (Medium)
5. **Dashboard → Add Messages** - dodaj poruke u queue
6. **Gledaj animaciju** - poruke se premještaju u kolone
7. **Review → HAM/SPAM** - označavaj gold labele
8. **Kad dostigneš threshold** - auto-retrain kreira novi model!
