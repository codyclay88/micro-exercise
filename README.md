# Micro-Burst Exercise Tracker

A zero-friction web app for logging short (2–5 minute) bursts of exercise — "exercise
snacking" / Greasing the Groove — so desk workers can record reps or seconds in a single
tap and get back to work, then review their accumulated volume over time.

Built as a single .NET solution: a **Blazor WebAssembly** SPA front end talking to an ASP.NET
Core **REST API** over the wire, **Entity Framework Core** over **PostgreSQL**, all in one
codebase. The server is stateless (no SignalR circuit) — it serves the API, the compiled WASM
client, and the server-rendered sign-in pages.

## Features

- **One-Click Log dashboard** (`/`) — each exercise is a Quick-Log Card; tap to log the
  configured target, or use the inline `+`/`-` steppers to micro-adjust a single burst.
  Shows per-card and total counts for today. **Keyboard hotkeys:** press `1`–`9` to log the
  matching card's target without the mouse (each card shows its keycap).
- **History** (`/history`) — review individual bursts over a date range; edit a burst's
  quantity and time, or delete it (with confirm).
- **Pool management** (`/pool`) — add exercises from the global catalog, give them a custom
  name and target, reorder them for dashboard priority, and remove them (soft delete —
  history is preserved).
- **Reports** (`/reports`) — date-range summary (7d / 30d / 90d quick ranges) with total
  volume, burst count, and average per burst for each exercise.
- **Dark mode** — toggle in the nav bar; respects system preference on first visit.

## Tech stack

| Layer | Choice |
|---|---|
| Runtime | .NET 10 |
| UI | Blazor **WebAssembly** SPA (`MicroExercise.Client`), Bootstrap 5.3 |
| API | ASP.NET Core Minimal APIs (stateless; also hosts the SPA + static SSR auth pages) |
| Data | Entity Framework Core 10 + PostgreSQL (Npgsql); SQLite in-memory for tests |
| Auth | Cookie authentication (HttpOnly/SameSite) |
| Tests | xUnit (EF Core SQLite in-memory) |

## Project structure

```
MicroExercise.slnx                     .NET 10 XML solution file
src/
  MicroExercise.Core/                  Domain + contracts (no external dependencies)
    Entities/  Enums/  Dtos/  Abstractions/
  MicroExercise.Infrastructure/        EF Core data access + service implementations
    Data/ (AppDbContext, Configurations, Migrations, SeedData)
    Services/ (PoolService, LogService, ReportService)
  MicroExercise.Client/                Blazor WebAssembly SPA (the UI)
    Pages/  Components/  Layout/  Services/ (typed API clients)  Authentication/
  MicroExercise.Web/                   Minimal API + SPA host + SSR auth (composition root)
    Components/Account/ (Login, Register)  Endpoints/  Authentication/
tests/
  MicroExercise.Tests/                 xUnit service tests
docs/                                  Product/technical spec
```

Dependencies point inward: **Web → Infrastructure → Core**, and **Client → Core** (shared
DTOs). Core has no framework dependencies, so the domain is testable and the DTOs are reused by
both the API and the WASM client. The browser runs the WASM client, which calls the REST API
with the auth cookie attached.

## Getting started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://docs.docker.com/get-docker/) (runs the local PostgreSQL — dev/prod parity)

### Run

```bash
# 1. Start a local PostgreSQL (published on host port 55432 to avoid clashing with any
#    system-installed Postgres on 5432).
docker compose -f compose.dev.yaml up -d

# 2. Run the app on the host (hot reload works as usual).
cd src/MicroExercise.Web
dotnet run
```

Then open **http://localhost:5077** (or `dotnet run --launch-profile https` for
**https://localhost:7020**).

On startup the app applies EF Core migrations (which seed the global exercise catalog) to the
PostgreSQL database. Register an account to get started — new sign-ups receive a few starter
exercises. The dev database lives in a Docker volume; `docker compose -f compose.dev.yaml down
-v` resets it. The dev connection string is in `appsettings.Development.json`; production
supplies its own via the `ConnectionStrings__AppDb` environment variable.

> **Authentication:** ASP.NET Core Identity with cookie authentication. Register at
> `/register` and sign in at `/login`. Email confirmation is disabled in the MVP (no mail
> service), so you can sign in immediately after registering.

### Test

```bash
dotnet test MicroExercise.slnx
```

## REST API

The WASM client talks to these endpoints over `HttpClient` (cookie auth). All require
authentication and are scoped to the current user; antiforgery is disabled on `/api` (the
client is same-origin JSON with a SameSite cookie — see the auth note below).

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/auth/me` | Current user's identity (lets the SPA establish auth state) |
| `GET` | `/api/exercises/pool` | Active pool for the dashboard grid |
| `GET` | `/api/exercises/types` | Global exercise catalog |
| `POST` | `/api/exercises/pool` | Add an exercise to the pool |
| `PUT` | `/api/exercises/pool/{id}` | Update custom name / target |
| `POST` | `/api/exercises/pool/{id}/move?up=true\|false` | Reorder |
| `DELETE` | `/api/exercises/pool/{id}` | Soft-delete a pool entry |
| `POST` | `/api/logs` | Record a burst |
| `GET` | `/api/logs?from=&to=` | Bursts in range (history) |
| `PUT` | `/api/logs/{id}` | Edit a burst's quantity + timestamp |
| `DELETE` | `/api/logs/{id}` | Delete a burst |
| `GET` | `/api/reports/summary?from=&to=` | Aggregated volume per exercise |

## Database

Core domain tables (see `docs/` for the full schema): `ExerciseTypes` (global lookups),
`ExercisePool` (per-user configured exercises, soft-deleted via `IsActive`), and
`WorkoutLogs` (transactional bursts), alongside the ASP.NET Core Identity tables
(`AspNetUsers` etc.). The app runs on **PostgreSQL** (Npgsql provider) in both development and
production for parity; the test suite uses an in-memory SQLite database (`EnsureCreated`,
provider-agnostic). The connection string comes from `ConnectionStrings:AppDb` —
`appsettings.Development.json` locally, the `ConnectionStrings__AppDb` environment variable in
production.

## Deployment

Production runs on a single DigitalOcean Droplet via Docker Compose: the app, PostgreSQL, and
a [Caddy](https://caddyserver.com/) reverse proxy (automatic Let's Encrypt HTTPS) all on one
box (~$6/mo). See `compose.yaml`, `Dockerfile`, and `Caddyfile`.

```bash
# On the Droplet (Docker + Compose installed):
git clone <repo> && cd MicroExercise
cp .env.example .env          # set APP_DOMAIN + a strong POSTGRES_PASSWORD
docker compose up -d --build  # builds the app, starts db + app + caddy
```

Point a DNS `A` record at the Droplet's IP and set it as `APP_DOMAIN`; Caddy issues the TLS
certificate automatically. Persistent state (PostgreSQL data + Data Protection keys) lives under
`DATA_DIR` — point it at an attached **DigitalOcean Block Storage volume** so the data survives a
Droplet rebuild. The database is backed up nightly via `scripts/backup.sh` (rotated locally +
optional off-site upload to DO Spaces); `scripts/restore.sh` restores a dump.

**See [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md)** for the full walkthrough — Droplet + swap,
block storage volume, deploy, backup cron, and restore.
