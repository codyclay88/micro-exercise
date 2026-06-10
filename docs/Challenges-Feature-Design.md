# Design Doc: Challenges (Shared Exercises) for Micro-Burst

**Status:** Proposal / scope assessment — *not committed to implementation.*
**Date:** 2026-06-10
**Depends on:** `docs/Goals-Feature-Design.md` (a Challenge materializes as a Goal)
**Related:** `docs/Micro-Burst Exercise Tracker Spec.md`, `docs/MCP-Server-Design.md`, `CLAUDE.md`

> **Purpose.** Let the system publish opt-in **Challenges** ("Push-up Challenge — 100
> pushups today") against a *shared* exercise, which a user accepts and tracks as a **Goal**
> on their own account. This doc also clarifies a misconception about the data model that
> shrinks the feature considerably.

---

## 1. Clarification: the shared exercise catalog already exists

The motivating premise — *"each user's exercises are unique records with no way to tie them
together"* — is **not** how the schema works today. There are two tables:

| Table | Role | Key fields |
|---|---|---|
| **`ExerciseType`** | The **global, shared catalog** | `Id`, `Name`, `DefaultTrackingType`, `OwnerUserId` (**null = global**, available to everyone) |
| **`ExercisePool`** | A user's **personal configuration** of a type | `UserId`, `ExerciseTypeId` (→ the shared type), `CustomName`, `TargetQuantity`, `SortOrder`, `IsActive` |

"Push-ups" is `ExerciseType.Id = 1`, seeded once with `OwnerUserId = null`. When two users
both have push-ups, their `ExercisePool` rows are distinct (different targets, names) **but
both reference `ExerciseTypeId = 1`.** That shared `ExerciseTypeId` *is* the concrete tie —
`PoolService`/`ReportService` already join through it, and the system can already group
users' pool rows by exercise type.

So **"a global exercise that each user configures to their own needs" is the existing
design**: `ExerciseType` = the global exercise, `ExercisePool` = the per-user config. No
restructuring is required. (`AddCustomExerciseAsync` is the one place a *private* type is
created, with `OwnerUserId = userId` — that's the per-user custom-exercise case, distinct
from the shared catalog.)

**Decision:** the "Bodyweight vs 2 Chains" per-user config enrichment is **deferred** (see
§8). This doc is purely about Challenges built on the catalog as it stands.

---

## 2. The model: Challenge (global) → Goal (per-user)

A **Challenge** is defined at the **`ExerciseType` (global) level**; opting in materializes a
**Goal** at the **`ExercisePool` (per-user) level**. This is why Goals were designed first.

```
Challenge (global)                         Goal (per-user)
┌──────────────────────────┐   accept     ┌────────────────────────────────┐
│ ExerciseTypeId  (= 1)     │ ───────────► │ UserId                          │
│ Title, Description        │              │ ExercisePoolId (user's push-ups)│
│ TargetQuantity (= 100)    │   per user   │ TargetQuantity = 100 (copied)   │
│ StartDate / Deadline      │              │ StartDate/Deadline (copied)     │
│ (one row, all users)      │              │ ChallengeId  ◄── links back     │
└──────────────────────────┘              └────────────────────────────────┘
```

- The Challenge carries the **target + window once**, shared by all participants; each
  participant's Goal copies them so progress is computed per-user against their own bursts.
- The Goal's `ChallengeId` links back, so **participants of a challenge = all Goals with that
  `ChallengeId`** (the basis for a future leaderboard).
- Progress, status (`Active`/`Achieved`/`Expired`), and percent all come from the Goals
  machinery for free — a challenge-derived goal is just a goal that happens to be tagged.

---

## 3. Decisions (locked)

- **Authorship:** Challenges are **system/admin-defined** (seeded, or created by an admin
  role / simple generator) and published for users to opt into. No user-facing authoring.
- **Per-user config enrichment:** **deferred** — build against existing `CustomName` +
  `TargetQuantity`; revisit resistance/equipment later (§8).
- **Leaderboard:** **design for it, build later** — tag goals with `ChallengeId` and store
  `UserId` directly on `Goal` so participation/ranking is queryable; defer the standings UI.

---

## 4. Schema

One new table; the `Goal` table gains two columns (already reflected in the amended
`Goals-Feature-Design.md`).

| Table | Column | Type | Notes |
|---|---|---|---|
| **Challenge** | Id | INT | PK, Identity |
| | ExerciseTypeId | INT | FK → ExerciseType(Id). Should reference a **global** type (`OwnerUserId == null`); validated on create. |
| | Title | VARCHAR(100) | e.g. "Push-up Challenge!" |
| | Description | VARCHAR(500)? | Optional, e.g. "Do 100 pushups today." |
| | TargetQuantity | INT | In the type's unit (`DefaultTrackingType`). > 0. |
| | StartDate | DATETIMEOFFSET | Challenge window start. |
| | Deadline | DATETIMEOFFSET | Challenge window end; > StartDate. |
| | IsPublished | BOOLEAN | Visibility flag (admin can stage before publishing). |
| | CreatedAt | DATETIMEOFFSET | Audit. |

**`Goal` additions** (see Goals doc): `UserId` (direct FK), `ChallengeId` (nullable FK →
Challenge), with a **filtered unique index on `(ChallengeId, UserId)`** so a user can't
join the same challenge twice.

No changes to `ExerciseType`, `ExercisePool`, or `WorkoutLog`.

---

## 5. The opt-in flow

`IChallengeService.AcceptChallengeAsync(int userId, int challengeId)`:

1. Load the challenge; reject if not published or past `Deadline`.
2. **Dedupe:** if a Goal already exists for `(challengeId, userId)`, return it (idempotent).
3. **Resolve the user's pool item** for `challenge.ExerciseTypeId`:
   - if the user has an active `ExercisePool` row for that type → use it;
   - if not → **auto-create** one from the global type (`UserId`, `ExerciseTypeId`,
     `TargetQuantity` = challenge target, next `SortOrder`, `IsActive = true`) so the user
     doesn't have to manually add it first;
   - if the user has **multiple** active pool rows for that type → pick the lowest
     `SortOrder` (and note the ambiguity — see §9).
4. **Create the Goal:** `UserId`, resolved `ExercisePoolId`, `TargetQuantity` /
   `StartDate` / `Deadline` copied from the challenge, `ChallengeId` set.
5. Return the `GoalDto`.

**Leaving:** `LeaveChallengeAsync(userId, challengeId)` deletes the linked Goal (goals are
hard-deletable per the Goals design). Re-joining recreates it.

---

## 6. Core contracts

```csharp
// Core/Dtos
public record ChallengeDto(
    int Id,
    int ExerciseTypeId,
    string ExerciseName,
    TrackingType TrackingType,
    string Title,
    string? Description,
    int TargetQuantity,
    DateTimeOffset StartDate,
    DateTimeOffset Deadline,
    ChallengeStatus Status,     // Upcoming | Active | Ended (computed from the window)
    bool IsJoined,              // does the current user have a Goal for it?
    int ParticipantCount);      // count of Goals with this ChallengeId (cheap; supports later leaderboard)

public record CreateChallengeRequest(   // admin-only
    int ExerciseTypeId,
    string Title,
    string? Description,
    int TargetQuantity,
    DateTimeOffset StartDate,
    DateTimeOffset Deadline);

// Core/Abstractions
public interface IChallengeService
{
    Task<IReadOnlyList<ChallengeDto>> GetChallengesAsync(int userId, bool includeEnded = false, CancellationToken ct = default);
    Task<ChallengeDto?> GetChallengeAsync(int userId, int challengeId, CancellationToken ct = default);
    Task<GoalDto?> AcceptChallengeAsync(int userId, int challengeId, CancellationToken ct = default);  // null if not found/unpublished/ended
    Task<bool> LeaveChallengeAsync(int userId, int challengeId, CancellationToken ct = default);

    // Authoring (admin role only — see §7)
    Task<ChallengeDto> CreateChallengeAsync(CreateChallengeRequest request, CancellationToken ct = default);
}
```

`ChallengeStatus` is a new enum, string-serialized per convention (like `TrackingType`).

---

## 7. API, authoring & UI

**Endpoints** (in `Web/Endpoints/ApiEndpoints.cs`, authorized `/api` group):

- `GET    /api/challenges?includeEnded=` → `ChallengeDto[]`
- `GET    /api/challenges/{id:int}` → `ChallengeDto`
- `POST   /api/challenges/{id:int}/accept` → `GoalDto` (201) / 404 / 409 if ended
- `DELETE /api/challenges/{id:int}/accept` → 204 (leave)
- `POST   /api/challenges` (`CreateChallengeRequest`) → **admin only** (`RequireAuthorization` with an `Admin` role policy)

**Authoring.** Identity is already configured with `IdentityRole<int>` + `AddRoles`, so an
`Admin` role gates `POST /api/challenges`. For the very first cut, challenges can simply be
**seeded** (migration `HasData`, or a startup seeder) — the admin endpoint can come later.
No new auth infrastructure either way.

**Client** (`MicroExercise.Client`, WASM):

- `ChallengeApi` typed client (alongside the existing `*Api` clients), `ApiJson.Options`.
- A `Challenges` page: list active challenges with `Title`, target, time remaining, and a
  **Join / Leave** button; joined challenges show progress (reuse the Goals progress view).
- Optional dashboard surfacing of active joined challenges next to the Goals strip.

---

## 8. Deferred: per-user configuration enrichment

The "Bodyweight vs 2 Chains" idea is a real but **separate** enhancement: a descriptive
`Resistance`/`Notes` field on `ExercisePool`. It's purely informational (a `WorkoutLog` is
still just a quantity, so it doesn't affect logging, reports, goals, or challenge progress),
which is exactly why it can be added independently later — a one-column migration plus a
field in the pool editor. **Out of scope for this doc** per the locked decision.

---

## 9. Scope assessment

**Overall size: small–medium, additive, gated on Goals.** No data-model overhaul — the
shared catalog already exists, so this is a new `Challenge` table + a thin service that
bridges to the already-designed Goals.

| Area | Change | Size |
|---|---|---|
| Schema + migration | `Challenge` table + `Goal` gains `UserId`/`ChallengeId` (folds into the Goal migration if built together) | Small |
| `Core` | `Challenge` entity, `ChallengeStatus` enum, `ChallengeDto`, `CreateChallengeRequest`, `IChallengeService` | Small |
| `Infrastructure` | `ChallengeService` (accept = find/auto-create pool item → create tagged Goal), EF config, DI, seed data | Medium |
| `Web` | 4 user endpoints + 1 admin endpoint (role-gated) | Small |
| `Client` | `ChallengeApi` + `Challenges` page (+ optional dashboard surfacing) | Medium (UI) |
| Tests | `ChallengeService` tests via `TestDb`: accept creates a tagged goal, auto-adds pool item when absent, dedupe, leave deletes | Small–Medium |
| Data model (`ExerciseType`/`ExercisePool`) | **None** | — |
| Auth / infra / deploy | **None** (reuses Identity roles for admin authoring) | — |

**Hard dependency:** Goals must exist first — a Challenge has no independent progress
tracking; it *is* a Goal once accepted. Build order: **Goals → Challenges.**

**Suggested build order within Challenges:** (1) `Challenge` entity + `IChallengeService`
(accept/leave/list) + seed one challenge + tests; (2) user endpoints; (3) `ChallengeApi` +
`Challenges` page; (4) admin authoring endpoint + role policy; (5) *(future)* leaderboard.

---

## 10. Synergy with the MCP proposal

Natural MCP tools once both features exist:

- `get_challenges(includeEnded?)` → `ChallengeDto[]` — **read-only**, could ship with the
  read tools.
- `accept_challenge(challengeId)` → `GoalDto` — **write**, belongs in the MCP mutating-tools
  phase (`mcp:write` scope).

This closes a nice loop: *"What challenges are available?" → "Join the push-up one"* → the
assistant accepts it, a tagged Goal appears, and the user logs bursts (via `log_burst`) that
advance it — all conversationally.

---

## 11. Open questions

- **Multiple pool items for one type.** If a user has two active push-up configurations,
  which does an accepted challenge attach to? Proposal: lowest `SortOrder`; consider letting
  the user choose at accept time.
- **Joining after start / before end.** Can a user join a challenge that's already underway
  (window already open)? Proposal: yes while `now < Deadline`; their backdated `StartDate`
  comes from the challenge, so prior in-window bursts count (consistent with Goals' backdating).
- **Challenge generation cadence.** If "the system generates" challenges, is that a seeded
  static set, an admin posting them, or an automated generator (e.g. weekly)? Affects only
  authoring, not the model.
- **Leaderboard privacy.** When standings are built: display names vs anonymous, opt-in vs
  automatic visibility.
