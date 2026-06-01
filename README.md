# Micro-Burst Exercise Tracker

A zero-friction web app for logging short (2–5 minute) bursts of exercise — "exercise
snacking" / Greasing the Groove — so desk workers can record reps or seconds in a single
tap and get back to work, then review their accumulated volume over time.

Built as a single .NET solution: an ASP.NET Core **Blazor Web App** front end and REST API,
**Entity Framework Core** over **SQLite**, all in one codebase.

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
| UI | Blazor Web App (Interactive Server render mode), Bootstrap 5.3 |
| API | ASP.NET Core Minimal APIs |
| Data | Entity Framework Core 10 + SQLite |
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
  MicroExercise.Web/                   Blazor UI + Minimal API (composition root)
    Components/ (Pages, Layout)  Endpoints/  Authentication/
tests/
  MicroExercise.Tests/                 xUnit service tests
docs/                                  Product/technical spec
```

Dependencies point inward: **Web → Infrastructure → Core**. Core has no framework
dependencies, so the domain is testable and the database is swappable.

## Getting started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Run

```bash
cd src/MicroExercise.Web
dotnet run
```

Then open **http://localhost:5077** (or `dotnet run --launch-profile https` for
**https://localhost:7020**).

On first run the app creates a local SQLite database (`microburst.db`) and applies
migrations (which seed the global exercise catalog). Register an account to get started —
new sign-ups receive a few starter exercises. The database file is git-ignored and rebuilt
from migrations if deleted.

> **Authentication:** ASP.NET Core Identity with cookie authentication. Register at
> `/register` and sign in at `/login`. Email confirmation is disabled in the MVP (no mail
> service), so you can sign in immediately after registering.

### Test

```bash
dotnet test MicroExercise.slnx
```

## REST API

All endpoints require authentication and are scoped to the current user.

| Method | Route | Purpose |
|---|---|---|
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
(`AspNetUsers` etc.). The connection string lives in
`src/MicroExercise.Web/appsettings.json` (`ConnectionStrings:AppDb`) and can be repointed to
SQL Server or PostgreSQL with a one-line provider change.
