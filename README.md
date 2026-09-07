# Board Game Platform

A production-oriented online multiplayer board game platform built on a .NET 9 microservice architecture with a React frontend.

Phase 1 delivered the full platform foundation: authentication, rooms/waiting rooms, game session infrastructure, a generic game engine, and real-time communication. The first game, **UNO**, is implemented on top of it — new games (Splendor, Wingspan, Azul, …) can be added without architectural changes.

---

## Technology Stack

| Layer | Technology |
|---|---|
| Backend | .NET 9, ASP.NET Core, C# |
| Frontend | React 18, TypeScript, Vite, Tailwind CSS, Zustand, TanStack Query |
| Real-time | SignalR |
| Data | PostgreSQL (one database per service), EF Core |
| Caching | Redis (room list cache, SignalR backplane, active game sessions) |
| Messaging | RabbitMQ (integration events + outbox pattern) |
| Gateway | YARP reverse proxy |
| Auth | JWT access tokens + refresh tokens (BCrypt password hashing) |
| Validation | FluentValidation (MediatR pipeline behaviors) |
| Logging | Serilog (structured) |
| API docs | Swagger/OpenAPI with XML comments |

---

## Architecture

```
Browser (React + Vite, :5173)
        │  /api/**, /hubs/**
        ▼
API Gateway (YARP, :5200)  ──  routes + forwards Authorization header
        │
        ├── /api/identity ──► Identity.Api (:5201)  ── identitydb
        ├── /api/lobby    ──► Lobby.Api    (:5202)  ── lobbydb
        ├── /api/game     ──► Game.Api     (:5203)  ── gamedb
        ├── /hubs/lobby   ──► Lobby.Api    (SignalR waiting-room updates)
        └── /hubs/game    ──► Game.Api     (SignalR live game updates)

Infrastructure (docker/postgres-redis-rabbitmq):
        PostgreSQL :5434   Redis :6380   RabbitMQ :5673 (UI :15673)
```

Every service owns its own database and is independently deployable. Services talk over REST (lobby → game session creation), SignalR (real-time), and RabbitMQ (integration events via the outbox: `UserRegistered`, `RoomCreated`, `RoomClosed`, `PlayerJoinedRoom`, `PlayerLeftRoom`, `GameStarted`, `GameFinished`).

### Repository layout

```
BoardGamePlatform/
├── backend/
│   └── src/
│       ├── BuildingBlocks/          # Shared kernel
│       │   ├── BuildingBlocks.Domain         # Result, Entity, exceptions
│       │   ├── BuildingBlocks.Application    # MediatR CQRS, pipeline behaviors, IUnitOfWork
│       │   ├── BuildingBlocks.Infrastructure # EF Core base DbContext, outbox, Redis, RabbitMQ, JWT, health checks
│       │   └── BuildingBlocks.Contracts      # Integration events, DTOs, SignalR client contracts
│       ├── Gateway/                 # YARP reverse proxy (no business logic)
│       ├── Services/
│       │   ├── Identity/   # Register, login, refresh, profile  (Domain/Application/Infrastructure/Api)
│       │   ├── Lobby/      # Rooms, ready state, host, start game (Domain/Application/Infrastructure/Api)
│       │   └── Game/       # Game sessions, actions, SignalR, turn timers
│       ├── GameEngine/
│       │   └── GameEngine.Core    # Pure C# game engine (IGame, GameState, GameAction) — no framework deps
│       └── Games/
│           ├── UNO/               # UNO rules implementing IGame
│           └── Splendor/          # Project stub for the next game
├── frontend/               # React + Vite SPA
├── docker/
│   ├── docker-compose.yml
│   └── db-init/            # Creates identitydb / lobbydb / gamedb on first startup
├── docs/
└── tests/
```

Key rules enforced by the architecture: no business logic in controllers or SignalR hubs, the Game Engine stays pure (no HTTP/EF/Redis), each service owns its own database, shared code lives only in BuildingBlocks.

---

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Node.js 18+ (for the frontend dev server)

### Run the full stack (Docker)

```bash
docker compose -f docker/docker-compose.yml up -d --build
```

This starts PostgreSQL, Redis, RabbitMQ, the three APIs, and the gateway. Databases and EF Core migrations are created automatically on first startup.

| Service | URL |
|---|---|
| API Gateway | http://localhost:5200 |
| Identity Swagger | http://localhost:5201/swagger |
| Lobby Swagger | http://localhost:5202/swagger |
| Game Swagger | http://localhost:5203/swagger |
| RabbitMQ UI | http://localhost:15673 (guest/guest) |

### Run the frontend

```bash
cd frontend
npm install
npm run dev
```

Open **http://localhost:5173**. The Vite dev server proxies `/api` and `/hubs` (including websockets) to the gateway at `http://localhost:5200`.

### Run the backend without Docker

Start only the infrastructure containers, then run the services from the solution:

```bash
docker compose -f docker/docker-compose.yml up -d postgres redis rabbitmq

dotnet build backend/BoardGamePlatform.sln
dotnet run --project backend/src/Services/Identity/Identity.Api
dotnet run --project backend/src/Services/Lobby/Lobby.Api
dotnet run --project backend/src/Services/Game/Game.Api
dotnet run --project backend/src/Gateway/Gateway
```

The gateway's `appsettings.json` points at `localhost:5201-5203`, which matches the default launch ports. Migrations apply automatically at startup.

### Useful commands

```bash
# Frontend type check / production build
cd frontend && npm run build

# Build the whole backend
dotnet build backend/BoardGamePlatform.sln

# Rebuild a single service image
docker compose -f docker/docker-compose.yml up -d --build game-api
```

---

## Features

### Identity Service
- Register, login, logout, refresh tokens, change password, profile update
- JWT authentication shared with all services via the gateway

### Lobby Service
- Create/join/leave rooms, ready/unready, kick player, transfer host, close room
- Start game (creates a game session in the Game service, stores `gameSessionId` on the room)
- Real-time waiting room over SignalR: player joined/left, ready changed, host changed, room closed, game started
- Redis-cached room list (only `Waiting` rooms are joinable/visible in the public list)
- Background cleanup of abandoned waiting rooms and expired closed rooms

### Game Service
- Game session lifecycle: create (from lobby), pause, resume, reconnect, finish
- Player actions processed through the Game Engine; updated `GameState` is the single canonical response (REST + SignalR broadcast)
- Redis-backed active session state, low-latency reads
- Turn timers with time banking, overrun penalties, auto-skip, and AFK elimination (`TurnTimeoutService`)
- Single-tab enforcement: a second connection for the same seat kicks the stale tab (`SessionTakenOver`)
- Takeover/join is idempotent per connection (React StrictMode and reconnect safe)

### Game Engine (pure C#)
- `IGame`: `CreateGame`, `ProcessAction`, `GetValidActions`, `IsGameOver`, `GetWinner`
- JSON-serializable `GameState`, `GameAction` with typed payloads, `GameResult`/`GameEvent`
- Zero dependencies on ASP.NET Core, EF Core, Redis, RabbitMQ, or SignalR
- Games are registered via DI (`Games/UNO` today, `Games/Splendor` prepared)

### Frontend
- Auth pages, lobby room list, room/waiting room with live updates, UNO game view
- Automatic token refresh, SignalR auto-reconnect, state via Zustand + TanStack Query

---

## API Overview

All endpoints are behind the gateway (`http://localhost:5200`). Interactive docs: append `/swagger` to each service's base URL.

**Identity** — `POST /api/identity/register`, `POST /api/identity/login`, `POST /api/identity/refresh`, `POST /api/identity/logout`, `GET|PUT /api/identity/me`, `POST /api/identity/change-password`

**Lobby** — `GET /api/lobby/rooms`, `POST /api/lobby/rooms`, `GET /api/lobby/rooms/{id}`, `POST /api/lobby/rooms/{id}/join | leave | ready | unready | start | close`, `POST /api/lobby/rooms/{id}/kick/{playerId}`, `POST /api/lobby/rooms/{id}/transfer-host/{playerId}`, SignalR hub `/hubs/lobby`

**Game** — `POST /api/game/sessions` (internal, called by Lobby), `GET /api/game/sessions/{id}`, `GET /api/game/sessions/{id}/state`, `POST /api/game/sessions/{id}/actions`, `POST /api/game/sessions/{id}/reconnect | pause | resume`, SignalR hub `/hubs/game`

---

## Configuration

Connection strings and infrastructure endpoints are read from `appsettings.json` / environment variables (double-underscore syntax, e.g. `ConnectionStrings__DefaultConnection`). Docker Compose wires everything automatically.

| Setting | Used by | Default (dev) |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | All services | PostgreSQL per service (`identitydb`, `lobbydb`, `gamedb`) |
| `Redis:Configuration` | All services | `localhost:6380` (host) / `redis:6379` (compose) |
| `RabbitMQ:Host` | All services | `localhost` (host) / `rabbitmq` (compose) |
| `Jwt:SigningKey` | Identity/Lobby/Game | Dev-only key — **must be replaced in production** |
| `Lobby:RoomCleanup:AbandonedRoomMinutes` | Lobby | `60` |
| `Lobby:RoomCleanup:ClosedRoomRetentionMinutes` | Lobby | `1440` |

Significant architectural and technical decisions are recorded in [`AGENTS.md`](../AGENTS.md) under **Critical Decisions** — that file is the single source of truth for the project spec.

---

## Roadmap

- Phase 2: first game fully polished (UNO), then Splendor
- Additional games as pure `IGame` implementations (Wingspan, Azul, Terraforming Mars, Ticket to Ride)
- Out of scope by design (for now): chat, friends, matchmaking, rankings, spectators, AI players

## License

TBD — not yet licensed for distribution.
