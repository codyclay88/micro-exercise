# CLAUDE.md

Guidance for AI agents working in this repo. See `README.md` for the human-facing overview
and `docs/Micro-Burst Exercise Tracker Spec.md` for the product/technical spec.

## What this is

Micro-Burst Exercise Tracker — a single .NET 10 solution (Blazor Web App + Minimal API +
EF Core/SQLite) for logging short exercise bursts. Clean Architecture layering.

## Commands

```bash
# Build / test the whole solution (note: .slnx, the .NET 10 XML solution format)
dotnet build MicroExercise.slnx
dotnet test  MicroExercise.slnx

# Run the app (from the Web project)
cd src/MicroExercise.Web && dotnet run     # http://localhost:5077  (https profile -> :7020)

# Add an EF Core migration — IMPORTANT: Infrastructure is its own startup project here
dotnet ef migrations add <Name> \
  --project src/MicroExercise.Infrastructure \
  --startup-project src/MicroExercise.Infrastructure \
  --output-dir Data/Migrations
```

There is no `.sln` — use `MicroExercise.slnx`.

## Architecture & layering (do not violate)

```
Web  ->  Infrastructure  ->  Core
 \_____________________________/
```

- **Core** (`src/MicroExercise.Core`) — entities, enums, DTOs, service interfaces. **No
  external/framework dependencies** (no EF Core, no ASP.NET). Keep it that way.
- **Infrastructure** (`src/MicroExercise.Infrastructure`) — `AppDbContext`, EF
  configurations, migrations, seeding, and the service *implementations* of Core's
  interfaces. References Core only.
- **Web** (`src/MicroExercise.Web`) — Blazor components, Minimal API endpoints, DI wiring,
  auth. The composition root; references both Core and Infrastructure.

Services are registered in `Infrastructure/DependencyInjection.cs` (`AddInfrastructure`).
API endpoints are mapped in `Web/Endpoints/ApiEndpoints.cs` (`MapApiEndpoints`).

Services take an explicit `int userId` parameter (not an ambient accessor) — keeps them
testable. The Web layer resolves the current user via `ICurrentUser` and passes it in.

## Conventions

- DTOs are `record`s in `Core/Dtos`. Service contracts are in `Core/Abstractions`.
- Ownership is always enforced in the service by filtering through
  `ExercisePool.UserId` (e.g. `l.ExercisePool!.UserId == userId`). Mutations return
  `null`/`false` when the entity isn't found or isn't owned; endpoints map that to 404.
- `ExercisePool` uses **soft delete** (`IsActive`) to preserve history. `WorkoutLog`
  deletes are **hard** (an accidental burst should truly disappear; keeps reports filter-free).
- Enums (`TrackingType`) are persisted as strings and serialized as strings in JSON.
- Blazor pages that need interactivity declare `@rendermode InteractiveServer`.

## Gotchas (learned the hard way)

- **SQLite + DateTimeOffset:** SQLite has no native date type, so `DateTimeOffset` range
  comparisons don't translate. `AppDbContext.ConfigureConventions` applies
  `DateTimeOffsetToBinaryConverter` (order-preserving long). A SQL Server/PostgreSQL switch
  would remove this.
- **EF `GroupBy` translation:** grouping by a multi-level navigation (e.g.
  `l.ExercisePool.ExerciseType.Name`) fails to translate. Flatten to scalar columns with a
  `.Select(...)` *before* `.GroupBy(...)` — see `ReportService.GetSummaryAsync`.
- **Layout components aren't interactive automatically:** a page's `@rendermode` does not
  flow to the layout around it. Components in `MainLayout` that need interactivity must set
  their own render mode (e.g. `<ThemeToggle @rendermode="InteractiveServer" />`).
- **EF migrations use Infrastructure as the startup project**, because the
  `Microsoft.EntityFrameworkCore.Design` package doesn't flow to Web (dev dependency) and a
  design-time `AppDbContextFactory` lives in Infrastructure.
- **Dev database is disposable:** `microburst.db*` is git-ignored; `MigrateAsync` runs on
  startup and the migration seeds the global exercise catalog (user data is created on
  registration). Delete the files to reset. If you change the model, delete the local DB (or
  add a migration) — startup `MigrateAsync` won't reconcile a stale schema.
- `dotnet-ef` tooling may warn it's older than the runtime (e.g. 10.0.0 vs 10.0.8) — cosmetic.

## Auth (ASP.NET Core Identity)

- `ApplicationUser : IdentityUser<int>` (Infrastructure/Identity) — **int keys** to match the
  schema. `AppDbContext` is an `IdentityDbContext<ApplicationUser, IdentityRole<int>, int>`.
- Register/Login/Logout are **static SSR** components in `Components/Account` (the auth cookie
  must be written on an HTTP request, not over the SignalR circuit). Registration seeds a few
  starter pool items, then signs in. Email confirmation is **off** (no mail service).
- The Identity application cookie's `LoginPath` is set to `/login` in `Program.cs` — the
  default would be `/Account/Login`. `[Authorize]` on a page is enforced at the endpoint
  (server-side), so an unauthenticated request is redirected by the cookie middleware before
  the `AuthorizeRouteView`/`RedirectToLogin` fallback runs.
- **Resolving the current user differs by context:** Blazor components read it from
  `AuthenticationStateProvider` / `AuthenticationState` (HttpContext is null in a circuit) via
  `ClaimsPrincipal.GetUserId()`; the HTTP API uses `ICurrentUser` (`HttpContextCurrentUser`)
  and the `/api` group has `.RequireAuthorization()`. Services themselves take an explicit
  `int userId` and stay auth-agnostic.

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
