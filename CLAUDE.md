# CLAUDE.md

Guidance for AI agents working in this repo. See `README.md` for the human-facing overview
and `docs/Micro-Burst Exercise Tracker Spec.md` for the product/technical spec.

## What this is

Micro-Burst Exercise Tracker — a single .NET 10 solution: a **Blazor WebAssembly** SPA talking
to an ASP.NET Core Minimal-API backend over HTTP, EF Core/PostgreSQL. Clean Architecture
layering. The server is **stateless — no SignalR/Blazor Server circuit**. Runs on PostgreSQL
(Npgsql) in dev and prod; tests use SQLite in-memory.

## Commands

```bash
# Build / test the whole solution (note: .slnx, the .NET 10 XML solution format)
dotnet build MicroExercise.slnx
dotnet test  MicroExercise.slnx

# Run the app — needs PostgreSQL. Start the dev DB first (published on host port 55432):
docker compose -f compose.dev.yaml up -d
cd src/MicroExercise.Web && dotnet run     # http://localhost:5077  (https profile -> :7020)

# MAUI mobile app (src/MicroExercise.Maui). On Windows the project builds the desktop target
# only (iOS/MacCatalyst need a Mac; Android needs the SDK/JDK configured) — see its csproj.
dotnet build src/MicroExercise.Maui/MicroExercise.Maui.csproj -f net10.0-windows10.0.19041.0
# Real mobile builds (on a configured machine): dotnet workload install maui; -f net10.0-android / -ios.

# Deploy the full production stack (app + Postgres + Caddy TLS) on a Droplet:
cp .env.example .env && docker compose up -d --build

# Add an EF Core migration — IMPORTANT: Infrastructure is its own startup project here
dotnet ef migrations add <Name> \
  --project src/MicroExercise.Infrastructure \
  --startup-project src/MicroExercise.Infrastructure \
  --output-dir Data/Migrations
```

There is no `.sln` — use `MicroExercise.slnx`.

## Architecture & layering (do not violate)

```
Client (WASM) ┐
              ├─HTTP─►  Web  ->  Infrastructure  ->  Core
Maui (native) ┘
   (both clients -> ApiClient -> Core; ApiClient holds the typed REST clients + DTOs)
```

- **Core** (`src/MicroExercise.Core`) — entities, enums, DTOs, service interfaces. **No
  external/framework dependencies** (no EF Core, no ASP.NET). Keep it that way.
- **Infrastructure** (`src/MicroExercise.Infrastructure`) — `AppDbContext`, EF
  configurations, migrations, seeding, and the service *implementations* of Core's
  interfaces. References Core only.
- **ApiClient** (`src/MicroExercise.ApiClient`) — the typed REST clients
  (`PoolApi`/`LogApi`/`ReportApi`/`GoalApi`) + `ApiJson` options. A plain library over
  `HttpClient`, referencing **Core only** (shared DTOs). Shared by the WASM `Client` and the
  future MAUI app so the data layer stays identical across front-ends (no view code here).
- **Client** (`src/MicroExercise.Client`) — the Blazor **WebAssembly** SPA (all the app UI:
  Dashboard/History/Pool/Reports + layout). Calls the REST API via the typed clients in
  `MicroExercise.ApiClient`; auth state via `CookieAuthStateProvider`.
  References **Core + ApiClient**. Runs in the browser — no server services here.
- **Maui** (`src/MicroExercise.Maui`) — the native **.NET MAUI** mobile/desktop app (MVVM +
  Shell). A *second* client of the same REST API, sharing **Core + ApiClient** verbatim; only
  the views (XAML + ViewModels) are platform-specific. Reuses the server's cookie auth from
  native code — `AuthService` drives the static-SSR `/login` form (GET for the antiforgery
  token, then form-POST) over an `HttpClient` with a shared `CookieContainer`, persists the
  Identity cookie to `SecureStorage`, and gates the Shell on `GET /api/auth/me`. **No server
  changes.** See `docs/MAUI-Mobile-App-Design.md`. Phase 1 (scaffold + auth) and Phase 2 (Log
  screen — the core logging loop, mirroring `Dashboard.razor`) are in; History/Reports/Goals/Pool
  remain placeholders pending Phases 3–4.
- **Web** (`src/MicroExercise.Web`) — Minimal API endpoints, the SPA host
  (`UseBlazorFrameworkFiles` + `MapFallbackToFile("index.html")`), static-SSR auth pages
  (`Components/Account`), DI, auth. Composition root; references Core, Infrastructure, **and
  Client** (to host it).

Services are registered in `Infrastructure/DependencyInjection.cs` (`AddInfrastructure`).
API endpoints are mapped in `Web/Endpoints/ApiEndpoints.cs` (`MapApiEndpoints`).

Services take an explicit `int userId` parameter (not an ambient accessor) — keeps them
testable. The API endpoints resolve the current user via `ICurrentUser` and pass it in; the
WASM client never sends a userId (the server derives it from the cookie).

## Conventions

- DTOs are `record`s in `Core/Dtos`. Service contracts are in `Core/Abstractions`.
- Ownership is always enforced in the service by filtering through
  `ExercisePool.UserId` (e.g. `l.ExercisePool!.UserId == userId`). Mutations return
  `null`/`false` when the entity isn't found or isn't owned; endpoints map that to 404.
- `ExercisePool` uses **soft delete** (`IsActive`) to preserve history. `WorkoutLog`
  deletes are **hard** (an accidental burst should truly disappear; keeps reports filter-free).
- Enums (`TrackingType`) are persisted as strings and serialized as strings in JSON. The WASM
  client's `HttpClient` uses `ApiJson.Options` (web defaults + `JsonStringEnumConverter`) to match.
- The UI is **all WebAssembly** — pages are in `Client/Pages`, call the typed API clients, and
  have **no `@rendermode`** (there's no Blazor Server render mode anymore). Page mutations reload
  via the API; there's no shared server-side component state.

## Gotchas (learned the hard way)

- **DateTimeOffset is provider-specific (`AppDbContext.ConfigureConventions`):** PostgreSQL
  maps `DateTimeOffset` to `timestamptz` and **rejects any non-UTC offset** (the app writes
  `DateTimeOffset.Now`), so the convention normalizes to UTC on write (`DateTimeOffsetUtcConverter`).
  SQLite (tests only) has no native date type, so it keeps `DateTimeOffsetToBinaryConverter`
  (order-preserving long). The branch is on `Database.IsSqlite()`. Both preserve instant-ordered
  range comparisons used by the date-range reports (spec §5.1).
- **EF `GroupBy` translation:** grouping by a multi-level navigation (e.g.
  `l.ExercisePool.ExerciseType.Name`) fails to translate. Flatten to scalar columns with a
  `.Select(...)` *before* `.GroupBy(...)` — see `ReportService.GetSummaryAsync`.
- **Hosting the WASM client:** use `app.UseBlazorFrameworkFiles()` + `app.UseStaticFiles()`
  **before** `app.UseRouting()`, then `app.MapFallbackToFile("index.html")` last. Do **not** use
  `MapStaticAssets()` — its fingerprinted endpoints can't serve the Blazor `_framework/*` files
  and it 500s. (So the server `App.razor` and `index.html` reference assets by plain path, not
  `@Assets`/`ImportMap`.)
- **Shared static assets** (bootstrap, `app.css`, `js/theme.js`, `js/hotkeys.js`, favicon) live
  in **`Web/wwwroot`** and are referenced by absolute path from `Client/wwwroot/index.html`
  (which holds only `index.html`). No duplication across projects.
- **`BlazorDisableThrowNavigationException`** is set in **both** Web and Client csproj — the
  client's `RedirectToLogin` does a `forceLoad` navigation that otherwise surfaces a benign
  `NavigationException` (and trips the `#blazor-error-ui` bar).
- **`#blazor-error-ui` CSS lives in `Web/wwwroot/app.css`** (shared). It must include
  `display:none` or the error bar shows permanently (the runtime toggles it on real errors).
- **EF migrations use Infrastructure as the startup project**, because the
  `Microsoft.EntityFrameworkCore.Design` package doesn't flow to Web (dev dependency) and a
  design-time `AppDbContextFactory` lives in Infrastructure.
- **Dev database is a disposable Postgres container:** `compose.dev.yaml` runs `postgres:17`
  on host port **55432** (avoids a system Postgres on 5432); `appsettings.Development.json`
  points at it. `MigrateAsync` runs on startup and the migration seeds the global exercise
  catalog (user data is created on registration). Reset with `docker compose -f compose.dev.yaml
  down -v`. If you change the model, add a migration — startup `MigrateAsync` won't reconcile a
  stale schema. EF migrations are **Npgsql-specific** now (one provider, one migration set).
- `dotnet-ef` tooling may warn it's older than the runtime (e.g. 10.0.0 vs 10.0.8) — cosmetic.

## Auth (ASP.NET Core Identity)

- `ApplicationUser : IdentityUser<int>` (Infrastructure/Identity) — **int keys** to match the
  schema. `AppDbContext` is an `IdentityDbContext<ApplicationUser, IdentityRole<int>, int>`.
- Register/Login/Logout are **static SSR** (server-rendered) in `Components/Account` + the
  logout endpoint in `Program.cs` — the auth cookie must be written on a real HTTP request, not
  from WASM. Registration seeds a few starter pool items, then signs in. Email confirmation is
  **off** (no mail service). The login/register forms keep antiforgery; logout `.DisableAntiforgery()`.
- The Identity application cookie's `LoginPath` is `/login`. **Cookie events return 401/403 for
  `/api`** (not a 302 redirect) — see `ConfigureApplicationCookie` `OnRedirectToLogin` — so the
  WASM client's `fetch` sees a clean 401 instead of silently following a redirect to HTML.
- **WASM auth state:** `CookieAuthStateProvider` (Client) calls `GET /api/auth/me` (rides the
  cookie) to build its `ClaimsPrincipal`; on 401 it's anonymous → `AuthorizeRouteView` →
  `RedirectToLogin` (forceLoad to `/login`). The HTTP API uses `ICurrentUser`
  (`HttpContextCurrentUser`); the `/api` group has `.RequireAuthorization()` and
  **`.DisableAntiforgery()`** (same-origin JSON + SameSite=Lax cookie is the CSRF posture).
  Services take an explicit `int userId` and stay auth-agnostic.

## Testing

xUnit in `tests/MicroExercise.Tests`. `TestDb` spins up a fresh SQLite in-memory database
per test (connection held open; `EnsureCreated` applies the model + `HasData`, so exercise
types 1–8 exist) and seeds a primary `ApplicationUser` (`TestDb.PrimaryUserId` == 1). Test
services directly by passing a `userId`.

## Conventions for changes

- One feature/milestone per commit; keep the build and tests green before committing.
- Update `docs/…Spec.md` when adding user-facing features or endpoints.
- Match the existing minimal styling (Bootstrap utilities + small scoped `.razor.css`); no
  Tailwind. Keep the product deliberately simple.
