# Design Doc: Goals Feature for Micro-Burst

**Status:** **Implemented (2026-06-10).** Schema, `GoalService`, `/api/goals` endpoints,
the `Goals` WASM page, and unit tests are in the tree (`AddGoals` migration). The optional
Dashboard strip (§7) was not built.
**Date:** 2026-06-10
**Related:** `docs/Micro-Burst Exercise Tracker Spec.md`, `docs/MCP-Server-Design.md`, `docs/Challenges-Feature-Design.md`, `CLAUDE.md`

> **Amendment (2026-06-10):** the `Goal` table carries `UserId` **directly** (rather than
> only via `ExercisePool.UserId`) to support the Challenges feature. The `ChallengeId`
> column described in §4 was **deferred** to the Challenges feature itself (it FKs a
> `Challenge` table that doesn't exist yet) — it will be added by that feature's migration,
> not this one. See `docs/Challenges-Feature-Design.md`.

> **Purpose.** Let a user set a target like *"100 pushups in the next three days"* and
> track progress toward it. This doc sizes the change and pins the design decisions.
> Unlike the MCP proposal, this is a small, self-contained feature that follows existing
> patterns one-for-one — no new infrastructure.

---

## 1. Goal & non-goals

**Goal.** A user can create a **one-shot, deadline-bound goal** against one of their
exercises (e.g. "100 push-ups by Friday"), and see live progress (current total, percent,
remaining, status) computed from the bursts they log.

**Non-goals (initial design).**

- Recurring/periodic goals ("50 pushups *every day*"). Deliberately out — see §9. The
  schema leaves room to add this later.
- Notifications / reminders (deadline approaching). The app has no mail service and stays
  stateless; goal status is surfaced in-app only.
- Streaks, badges, social/sharing. Out of scope for the deliberately-minimal product.

---

## 2. The shape of a goal

A goal is **an exercise + a target amount + a time window**. "100 pushups in the next
three days" decomposes to:

| Part | Value | Source |
|---|---|---|
| target exercise | a **pool item** | `ExercisePool` (what `WorkoutLog.ExercisePoolId` already points at) |
| unit | reps or seconds | the pool item's `TrackingType` — inherited, never ambiguous |
| target quantity | `100` | user input |
| window start | user-selectable, defaults to now | `StartDate` |
| deadline | now + 3 days | `Deadline` |

**Progress requires no new tracking data.** It is:

```
SUM(WorkoutLog.CompletedQuantity)
WHERE WorkoutLog.ExercisePoolId = goal.ExercisePoolId
  AND WorkoutLog.Timestamp >= goal.StartDate
  AND WorkoutLog.Timestamp <= goal.Deadline
```

This is the same date-range aggregation `ReportService.GetSummaryAsync` already performs
(spec §5.1) — reuse its conventions: **flatten multi-level navigations to scalar columns
with `.Select(...)` before `.GroupBy(...)`** (EF translation gotcha), and rely on the
provider-specific `DateTimeOffset` UTC normalization for instant-ordered range comparisons.

---

## 3. Design decisions (locked)

- **Goal type:** one-shot deadline goals only. (Recurring deferred — §9.)
- **Progress start:** `StartDate` defaults to "now" at creation, but the user may pick an
  earlier start (e.g. start of today) so bursts already logged today can count. Progress
  always counts logs with `Timestamp` in `[StartDate, Deadline]`.
- **Target granularity:** a **pool item**, not a catalog exercise type. Consistent with how
  logs and reports already work; the unit comes for free; ownership is enforced the same way.

---

## 4. Schema

One new table. No changes to the existing four.

| Table | Column | Type | Notes |
|---|---|---|---|
| **Goal** | Id | INT | PK, Identity |
| | UserId | INT | FK → User(Id), stored **directly**. (Originally proposed as ownership-via-`ExercisePool.UserId`; promoted to a real column to support Challenge participation dedupe/queries.) |
| | ExercisePoolId | INT | FK → ExercisePool(Id). The targeted exercise; ownership still cross-checked against `UserId`. |
| | TargetQuantity | INT | In the pool item's unit (reps or seconds). Must be > 0. |
| | StartDate | DATETIMEOFFSET | Window start; defaults to creation time, may be backdated. |
| | Deadline | DATETIMEOFFSET | Window end; must be > StartDate. |
| | ChallengeId | INT? | Nullable FK → Challenge(Id). Null for a self-set goal; set when the goal was created by opting into a Challenge. Unique per `(ChallengeId, UserId)`. See `docs/Challenges-Feature-Design.md`. |
| | CreatedAt | DATETIMEOFFSET | Audit. |

**Delete semantics:** **hard delete**, like `WorkoutLog` (a goal is transient/personal).
Achieved and expired goals are **not** auto-purged — status is computed, so they linger as
read-only history until the user deletes them. (No `IsActive`/soft-delete needed; goals
don't anchor historical reports the way `ExercisePool` does.)

**Optional:** an `AchievedAt` (nullable DATETIMEOFFSET) could be stored the first time a
goal crosses its target, to record *when* it was hit rather than just *that* it was. Not
required since status is computed; include only if "achieved on day 2 of 3" matters.

---

## 5. Status (computed, not stored)

Derived on read from progress vs. target vs. clock:

| Status | Condition |
|---|---|
| `Achieved` | `CurrentProgress >= TargetQuantity` |
| `Active` | not achieved **and** `now <= Deadline` |
| `Expired` | not achieved **and** `now > Deadline` |

(If `AchievedAt` is stored, `Achieved` is sticky even if logs are later deleted; otherwise
it's purely a function of current progress.)

---

## 6. Core contracts

```csharp
// Core/Dtos
public record GoalDto(
    int Id,
    int ExercisePoolId,
    string ExerciseName,        // resolved CustomName ?? ExerciseType.Name
    TrackingType TrackingType,
    int TargetQuantity,
    int CurrentProgress,
    int RemainingQuantity,      // max(0, Target - Current)
    double PercentComplete,     // clamped 0..100
    DateTimeOffset StartDate,
    DateTimeOffset Deadline,
    GoalStatus Status);         // Active | Achieved | Expired (string-serialized, like TrackingType)

public record CreateGoalRequest(
    int ExercisePoolId,
    int TargetQuantity,
    DateTimeOffset Deadline,
    DateTimeOffset? StartDate);  // null => now

// Core/Abstractions
public interface IGoalService
{
    Task<GoalDto> CreateGoalAsync(int userId, CreateGoalRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<GoalDto>> GetGoalsAsync(int userId, bool includeCompleted = true, CancellationToken ct = default);
    Task<GoalDto?> GetGoalAsync(int userId, int goalId, CancellationToken ct = default);
    Task<bool> DeleteGoalAsync(int userId, int goalId, CancellationToken ct = default);
}
```

`GoalStatus` is a new enum (persisted as string per convention — though it's only ever
*computed*, so it never actually persists; it serializes as a string in JSON to match
`TrackingType`). Mutations return `null`/`false` when the goal isn't found or isn't owned;
endpoints map that to 404 — same convention as the existing services.

**Listing efficiency:** computing progress for N goals is N sums, or one grouped query over
`WorkoutLog` keyed by `ExercisePoolId` and filtered to the union of windows. Start with the
straightforward per-goal sum; optimize to a single grouped query (flatten-before-`GroupBy`)
only if a user accumulates many concurrent goals.

---

## 7. API & UI

**Endpoints** (in `Web/Endpoints/ApiEndpoints.cs`, under the authorized `/api` group):

- `GET  /api/goals?includeCompleted=true|false` → `IReadOnlyList<GoalDto>`
- `POST /api/goals` (`CreateGoalRequest`) → `GoalDto` (201) / `ValidationProblem` / 404 if pool item not owned
- `GET  /api/goals/{id:int}` → `GoalDto` / 404
- `DELETE /api/goals/{id:int}` → 204 / 404

**Validation** (in the service): `TargetQuantity > 0`; `Deadline > StartDate`; the
`ExercisePoolId` must resolve to an **active, owned** pool item.

**Client** (`MicroExercise.Client`, all WASM — no `@rendermode`):

- New `GoalApi` typed client in `Client/Services` (alongside `PoolApi`/`LogApi`/`ReportApi`),
  using `ApiJson.Options`.
- New `Goals` page in `Client/Pages`: list active goals with compact progress bars
  (current / target, % , days left), a create form (pick pool item → target → deadline →
  optional start), and a delete action. Achieved/expired goals shown in a collapsed history
  section.
- Optionally surface active goals on the **Dashboard** as a thin strip above the logging
  grid, so progress is visible at the moment of logging. (Keep minimal per the product's
  zero-friction philosophy.)

---

## 8. Scope assessment

**Overall size: small, fully within existing patterns.** No new infrastructure, no new
external dependencies, no auth changes. It's a vertical slice that mirrors the existing
Pool/Log/Report stack.

| Area | Change | Size |
|---|---|---|
| Schema + migration | One `Goal` table; one EF migration (Infrastructure as startup project) | Small |
| `Core` | `Goal` entity, `GoalStatus` enum, `GoalDto`, `CreateGoalRequest`, `IGoalService` | Small |
| `Infrastructure` | `GoalService` (reuses Report's date-range aggregation pattern), EF config, DI registration | Small–Medium |
| `Web` | 4 endpoints in `ApiEndpoints.cs` | Small |
| `Client` | `GoalApi` + `Goals` page (+ optional Dashboard strip) | Medium (mostly UI) |
| Tests | `GoalService` tests via `TestDb` (seed pool + logs in/out of window, assert progress/status) | Small |
| Auth / infra / deploy | **None** | — |

**What makes it cheap:** progress is derived from data already captured; the date-range
aggregation and `DateTimeOffset` handling are solved problems in `ReportService`; the
service signature (`int userId` first) and ownership-via-`ExercisePool.UserId` conventions
drop straight in. Biggest single chunk is the `Goals` page UI.

**Suggested build order:** (1) schema + `IGoalService`/`GoalService` + tests;
(2) API endpoints; (3) `GoalApi` + `Goals` page; (4) optional Dashboard strip.

---

## 9. Synergy with the MCP proposal

The two features reinforce each other. Parsing *"I want to do 100 pushups in the next
three days"* into `{exercisePoolId, targetQuantity, deadline}` is exactly what an AI
assistant does well: it can resolve "pushups" to the user's pool item (via `list_pool`)
and "next three days" to a concrete `Deadline`. The API stays dumb — it takes a resolved
target + deadline — and the assistant does the NL→structured translation.

If the MCP server (see `MCP-Server-Design.md`) is built, this adds two natural tools:

- `create_goal(exercisePoolId, targetQuantity, deadline, startDate?)` → `GoalDto`
- `get_goals(includeCompleted?)` → `GoalDto[]`

`create_goal` would be a **write** tool, so it belongs in the MCP "mutating tools" phase
(behind the `mcp:write` scope), not the initial read+log surface. `get_goals` is read-only
and could ship with the read tools.

---

## 10. Edge cases & decisions

- **Backdated start counts prior logs.** With a user-chosen `StartDate` earlier than now,
  bursts already in that window count immediately — a goal can be partially (or fully)
  complete the moment it's created. Intended.
- **Soft-deleted pool item with an active goal.** The goal remains and still shows
  historical progress, but the user can no longer log against a deactivated pool item, so it
  effectively can't advance. Acceptable; the goal can be deleted. (Creation validates the
  pool item is active; deactivation afterward is allowed.)
- **Multiple concurrent goals for the same exercise.** Allowed; each computes independently
  over its own window.
- **Deadline in the past at creation.** Rejected by validation (`Deadline > StartDate`).
- **Exercise renamed** (`CustomName` changed) after goal creation. `GoalDto.ExerciseName`
  is resolved on read, so it always reflects the current name.
- **Timezones.** Windows are instants (`DateTimeOffset`); range comparisons inherit the
  provider-specific UTC normalization already in `AppDbContext.ConfigureConventions`.

---

## 11. Open questions

- **Store `AchievedAt`?** Only needed if "when it was hit" matters or if `Achieved` must be
  sticky against later log deletions. Default: skip, compute status purely from progress.
- **Dashboard surfacing.** Worth the (small) added density on the zero-friction dashboard,
  or keep goals on their own page? Leaning toward a thin collapsible strip.
- **History retention.** Keep expired/achieved goals indefinitely until manual delete
  (current proposal), or auto-archive after some period?
