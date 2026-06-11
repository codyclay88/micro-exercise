# Design Doc: .NET MAUI Mobile App for Micro-Burst

**Status:** Proposal / scope assessment — *not committed to implementation.*
**Author:** (design exploration)
**Date:** 2026-06-11
**Related:** `docs/Micro-Burst Exercise Tracker Spec.md`, `CLAUDE.md`

> **Purpose of this doc.** Capture what it would take to ship a native **.NET MAUI**
> mobile app that mirrors the existing Blazor WebAssembly front-end's functionality, plus
> an **export of logged bursts into Apple Health / Android Health Connect**. This is
> written to **size the change** and lock the architectural choices, not to dictate
> line-level implementation. §8 (Scope Assessment) is the part to read if you only read one
> section. §9 (Health Export) is self-contained and can be deferred to a later phase.

---

## 1. Goal & non-goals

**Goal.** A native MAUI app (iOS + Android first) that lets an authenticated user do
everything the web SPA does today, against the *same* REST API and the *same* account:

- **Log** a micro-burst from a quick-log screen (the core daily loop).
- Manage their **Pool** (add/edit/reorder/deactivate, add custom exercises).
- Review **History** (date-range list, edit/delete a burst).
- See **Reports** (volume summary over a range).
- Create and track **Goals** (deadline-bound targets with live progress).
- *(Phase 6)* **Export** logged bursts into Apple Health / Health Connect.

**Non-goals (for the initial design).**

- Changing the server, REST API, `Core`, or `Infrastructure` in any way. The mobile app is
  a **new client** of the existing `/api`. (The only conceivable server touch — bearer
  auth — was explicitly ruled out; see §4.)
- Offline-first / local persistence of bursts. The app is online-first like the SPA; a
  light cache is a future nice-to-have, not v1.
- Sharing the *UI* with the web. We chose **native XAML**, not Blazor Hybrid, so views are
  rebuilt; only the data layer and DTOs are shared (§3).
- **Reading** data back from Health. Export is **one-way** (§9).

---

## 2. Why native MAUI (and what that costs)

The decision was **native XAML + MVVM**, not MAUI Blazor Hybrid. The tradeoff, made
deliberately:

- **Win:** native look/feel, gestures, platform integration — and, critically, **direct
  access to HealthKit / Health Connect**, which a WebView-hosted Blazor app cannot reach.
- **Cost:** the five screens are **rewritten** as XAML + ViewModels rather than reused from
  the Razor components. That introduces **parity drift** risk — the mobile UI and web UI can
  diverge over time.

The mitigation for drift is to share *everything below the view*: DTOs, the typed API
clients, and JSON config (§3). Only the presentation layer is per-platform.

---

## 3. What is shared vs. rewritten

The app already has a clean seam: every screen's behavior is "call a typed API client, bind
the resulting DTOs." That seam is exactly what crosses platforms.

| Layer | Web (`Client`) | MAUI | Reuse? |
|---|---|---|---|
| DTOs / enums | `Core/Dtos`, `TrackingType`, `GoalStatus` | same | ✅ verbatim (reference `Core`) |
| Typed API clients | `PoolApi` / `LogApi` / `ReportApi` / `GoalApi` | same shape | ♻️ **extract to shared lib** (below) |
| JSON config | `ApiJson.Options` (web defaults + `JsonStringEnumConverter`) | same | ♻️ shared |
| Auth transport | cookie via SSR `/login` form | cookie via `CookieContainer` | logic reused, transport new |
| UI (pages/components) | Razor + Bootstrap | XAML + ViewModels | ❌ rewritten |

### 3.1 Recommended refactor — extract a shared API-client library

Lift the four typed clients + `ApiJson` out of `Client/Services` into a new
**`MicroExercise.ApiClient`** library (depends on `HttpClient` + `Core`), referenced by
**both** the WASM `Client` and the new `Maui` project.

- **Why:** it is the single biggest defense against the parity-drift cost of going native.
  The data layer stays *identical* for web and mobile; only views differ. A new endpoint or
  a contract change is made once.
- **Fallback if we don't want to touch `Client` now:** copy the four small client classes
  (~4 files) into the MAUI project and accept manual sync. Cheaper to start, more drift
  later. The shared-lib refactor is strongly recommended as **step 0**.

**Consequence either way: no changes to `Core` or `Infrastructure`.** The clients already
target `HttpClient`; only the `HttpClient`'s *handler* (cookie container) and *base address*
differ between web and mobile.

---

## 4. Authentication — reuse the existing cookie

The web uses an HttpOnly Identity cookie written by the **server-rendered `/login`** form
and read back via `GET /api/auth/me`. The mobile app **reuses that cookie** rather than
adding bearer/JWT auth — **no server changes**.

- **One `HttpClient`** built on `HttpClientHandler { CookieContainer, UseCookies = true }`,
  registered as a singleton; base address from config (§7).
- **Login (native form POST, preferred):** `GET /login` to obtain the antiforgery cookie +
  hidden `__RequestVerificationToken`, then `POST` email/password + token as
  `application/x-www-form-urlencoded`. The server writes the
  `.AspNetCore.Identity.Application` cookie into our `CookieContainer`. This keeps a fully
  native login page — no embedded browser.
  - *Fallback:* a `WebAuthenticator` / embedded `WebView` pointed at `/login`, then harvest
    the cookie. Simpler to wire, less-native UX, and cookie extraction is platform-specific.
- **Persisted session:** serialize the Identity cookie into `SecureStorage`; rehydrate the
  `CookieContainer` on launch so users stay signed in (the cookie is 30-day sliding).
- **Identity hydration:** on launch call `GET /api/auth/me` → `CurrentUserDto`. 200 ⇒ enter
  the app shell; 401 ⇒ show login.
- **Logout:** clear `CookieContainer` + `SecureStorage` (optionally `POST /account/logout`).

**CSRF posture is unchanged and safe.** The `/api` group already has `DisableAntiforgery()`,
and from a native client the cookie is first-party to the API host, so `SameSite=Lax` is not
a cross-site obstacle for a native `HttpClient`. The one wrinkle is driving the
antiforgery-protected SSR form from native code (handled above; WebView is the escape hatch).

---

## 5. App structure (MVVM + Shell)

- **`AppShell`** with a bottom `TabBar` mirroring the web `NavMenu`:
  **Log · History · Reports · Goals · Pool**. An auth gate routes to a `LoginPage` when
  `/api/auth/me` is anonymous.
- **ViewModels** (one per screen; `CommunityToolkit.Mvvm` source-gen
  `ObservableObject`/`RelayCommand`):

  | ViewModel | Mirrors web page | Key behavior |
  |---|---|---|
  | `DashboardViewModel` | `Dashboard.razor` (`/`) | load active pool + today's bursts; `LogCommand(item, qty)`; per-item today totals; active-goal progress strip |
  | `HistoryViewModel` | `History.razor` | date-range pickers (default last 7d); list `BurstDto`; edit (`PUT /api/logs/{id}`) / delete |
  | `ReportsViewModel` | `Reports.razor` | range presets (7/30/90d); `ExerciseSummaryDto` list |
  | `GoalsViewModel` | `Goals.razor` | active vs completed/expired; create/delete; live progress |
  | `PoolViewModel` | `Pool.razor` | list/add/edit/reorder(move up/down)/deactivate; add custom exercise; pick from `ExerciseTypeDto` catalog |
  | `LoginViewModel` + `ISession` | (SSR login) | form-POST login, current-user state |

- **Views (XAML):** `CollectionView` lists; a reusable **`QuickLogCard`** (the native twin
  of the web component) with a +/- stepper and a big log button; `DatePicker`s; progress via
  `ProgressBar`. A value converter turns `TrackingType` into "reps" vs "seconds" labels.
- **Mobile-appropriate substitutions for web affordances:** the web's 1-9 **hotkeys** and
  **grid/list toggle** become large tap targets and a single scrollable list — no direct
  mobile analog needed.
- **Theming:** honor the existing dark-mode preference with `AppThemeBinding` + a palette
  `ResourceDictionary`, and a theme toggle mirroring `ThemeToggle.razor`.

---

## 6. Functional parity checklist (against the existing API)

All endpoints already exist and are unchanged:

- **Log:** `POST /api/logs` with optimistic today-total update; handle the `null`/404
  "pool item not owned/active" case.
- **Pool:** `GET /api/exercises/pool`, `GET /api/exercises/types`,
  `POST /api/exercises/pool`, `POST /api/exercises/custom`,
  `PUT /api/exercises/pool/{id}`, `POST /api/exercises/pool/{id}/move?up=`,
  `DELETE /api/exercises/pool/{id}`.
- **History:** `GET /api/logs?from=&to=`, `PUT /api/logs/{id}`, `DELETE /api/logs/{id}`.
- **Reports:** `GET /api/reports/summary?from=&to=`.
- **Goals:** `GET /api/goals?includeCompleted=`, `POST /api/goals`,
  `DELETE /api/goals/{id}` (display `PercentComplete` / `RemainingQuantity`).
- **Auth:** `GET /api/auth/me`.

---

## 7. Configuration, tooling, testing

- **Base URL / loopback quirk:** prod `https://exercise.codyclay.com`; dev needs the
  platform loopback — **Android emulator** uses `http://10.0.2.2:5077`, **iOS simulator**
  uses `localhost`. Put this behind a `BackendOptions` / build-config switch.
- **Project:** `src/MicroExercise.Maui`, TargetFrameworks
  `net10.0-android;net10.0-ios;net10.0-maccatalyst` (add `-windows` only for desktop debug).
  Register in `MicroExercise.slnx`. `ProjectReference` → `Core` (+ the shared `ApiClient`
  lib per §3.1). Packages: `CommunityToolkit.Mvvm`, optionally `CommunityToolkit.Maui`.
- **Build prerequisites** (document under `## Commands` in `CLAUDE.md`):
  `dotnet workload install maui`; Android needs JDK + Android SDK; **iOS builds require
  macOS**.
- **Tests:** unit-test the ViewModels against a faked API client (the native UI is thin;
  logic lives in VMs). Can extend `MicroExercise.Tests` or add a dedicated MAUI test project.
- **Assets:** app icon, splash, Android `minSdk` / iOS target versions, store metadata.

---

## 8. Scope assessment

**Overall size: moderate–large, concentrated in UI rebuild and platform plumbing, not in
business logic.** Nothing on the server moves.

| Area | Change | Size |
|---|---|---|
| `MicroExercise.ApiClient` (new shared lib) | Extract 4 typed clients + `ApiJson` from `Client` | Small |
| `MicroExercise.Maui` — shell + auth | Project, `AppShell`/tabs, native login (form POST), `/api/auth/me` gate, persisted cookie | **Medium (auth is the trickiest part)** |
| `MicroExercise.Maui` — 5 screens | XAML views + ViewModels for Log/History/Reports/Goals/Pool | **Largest piece** |
| Theming / assets / packaging | Dark theme, icons, splash, store setup | Medium |
| Health export (§9) | Platform abstraction + iOS/Android writers | **Separate phase; own risks** |
| `Core` / `Infrastructure` / REST API / `Client` logic | **None** (shared lib is a lift, not a rewrite) | — |
| Server / deployment | **None** | — |

**What makes it cheap:** the service/REST seam already exists; DTOs and clients are reused
verbatim; cookie auth needs no server work.

**What carries the risk:**
1. **Driving the antiforgery SSR `/login` form from native code** (§4) — WebView fallback
   exists.
2. **UI parity drift** — mitigated by the shared `ApiClient` lib (§3.1).
3. **Health Connect binding availability** (§9) — the biggest unknown; isolated to Phase 6.

**Rough sequencing (build order, if pursued):**

1. **Shared `ApiClient` lib** — extract from `Client`; web still green. *(Step 0, de-risks
   drift.)*
2. **Scaffold + auth** — project, Shell, native login, `/api/auth/me` gate, persisted
   cookie. *(Proves the hardest non-Health part first.)*
3. **Log screen** — pool load + logging (the core daily loop).
4. **History + Reports.**
5. **Goals + Pool management.**
6. **Polish** — dark theme, error/empty states, icons, packaging.
7. *(Optional)* **Health export** — see §9; behind a feature flag.

---

## 9. Health export (Apple Health + Android Health Connect)

**Decisions locked:** **export-only** (one-way, app → Health), **daily-aggregate per
exercise**, **client-side** (no server or `Core` changes). The app already holds every burst
via `GET /api/logs`; the exporter reads those, rolls them up per exercise per local day, and
writes one workout per exercise/day into the platform Health store. Only reachable from
native code — another point in favor of native MAUI.

### 9.1 Platform abstraction (keeps MVVM clean)

`IHealthExportService` with platform partial implementations via MAUI multi-targeting
(`Platforms/iOS`, `Platforms/Android`); **Windows / Mac Catalyst = no-op**, so the DI graph
never branches.

```
Task<bool>             IsSupportedAsync()
Task<HealthAuthStatus> RequestAuthorizationAsync()
Task                   SyncDayAsync(DateOnly day)          // upsert one day's aggregates
Task                   SyncRangeAsync(DateOnly from, to)   // backfill
Task                   DisconnectAsync()
```

### 9.2 Aggregation model

- Group bursts by **`ExercisePoolId` + local calendar day**; sum `Quantity`.
- Workout time span = first → last burst of that exercise that day. For `Seconds` exercises
  the **duration = total seconds**; for `Reps`, use the burst span (or a nominal short
  duration) and carry the rep count as data.
- **One Health record per (exercise, day).** Re-running for the same day **replaces** that
  record, so edits/deletes (on web or mobile) reconcile cleanly.

### 9.3 Data mapping (the genuinely tricky part)

Neither store has a first-class "reps" concept:

- **Apple HealthKit:** write an `HKWorkout` / `HKWorkoutBuilder` with an
  `HKWorkoutActivityType`; put **total reps in workout metadata** (no native rep quantity).
  `Seconds` exercises map naturally to workout duration.
- **Health Connect:** write an `ExerciseSessionRecord` with an `ExerciseType`; for strength
  exercises attach an `ExerciseSegment` carrying `repetitions`. `Seconds` exercises (e.g.
  plank) map to a duration session.
- A small **exercise → activity-type mapping table** (pushups / squats / plank / …) with a
  default fallback (`functionalStrengthTraining` / `EXERCISE_TYPE_STRENGTH_TRAINING`).
  `TrackingType` selects rep-count vs duration handling. We have **no calorie data — omit
  energy** rather than fabricate it. A per-exercise override of the mapping can come later.

### 9.4 Idempotency / dedup

Deterministic key per record: `hash(userId, exercisePoolId, day)`.

- **Health Connect:** set it as `clientRecordId` → the platform **upserts** natively.
- **HealthKit:** no upsert — keep a **local sync ledger** (key → `HKWorkout` UUID + content
  hash) in `Preferences` / `SecureStorage`; on change, delete the old workout and write the
  new one. The ledger also makes "is this day already synced?" cheap.

Because aggregation is by **local day**, edits/deletes to past bursts simply re-aggregate
and upsert that day.

### 9.5 Sync triggers & UX

- **First run:** a Settings toggle "Sync to Apple Health / Health Connect" triggers the
  permission prompt; show connection status + "last synced".
- **Ongoing:** upsert **today's** aggregate whenever a burst is logged or the app
  backgrounds (cheap, idempotent). Provide a manual **"Sync now"** and a **backfill range**
  for first-time / full export.
- **Disconnect:** revoke/forget + stop writing. Note honestly that platforms don't always
  let an app delete prior records on revoke.
- *(Optional later)* periodic background sync via platform background-task APIs — not
  required for v1 given on-log / on-background upsert.

### 9.6 Permissions, entitlements, store review (plan for the friction)

- **iOS:** HealthKit **entitlement** + capability in the provisioning profile;
  `NSHealthUpdateUsageDescription` in Info.plist (write-only ⇒ only the *Update* string; no
  Share/read string needed). App Store review scrutinizes Health usage — needs a clear
  justification string.
- **Android Health Connect:** declare `android.permission.health.WRITE_EXERCISE` (+ any
  segment perms), a **permissions-rationale activity**, and a **published privacy policy
  URL**; Play Console **Data safety** form + a **Health Connect access review**. Runtime:
  Android 14+ has it built-in; Android 13 needs the Health Connect app installed — detect and
  prompt.
- **Technical risk to flag now:** there may be **no official .NET binding** for the Health
  Connect Jetpack client (`androidx.health.connect:connect-client`) — budget for a **binding
  project** or vetting a community plugin. HealthKit is well covered by the .NET iOS
  bindings. **Spike this early.**

### 9.7 Health sub-sequencing

1. `IHealthExportService` + Settings toggle/permissions.
2. **iOS HealthKit writer** first (lower binding risk — proves the model).
3. **Android Health Connect writer** (after the binding spike).
4. Aggregation + idempotency ledger.
5. Backfill + on-log / on-background upsert.

Keep the whole feature **behind a feature flag** so the app ships without it if store review
or the Android binding slips.

---

## 10. Open questions

- **Shared `ApiClient` refactor now or later?** Doing it as step 0 minimizes drift but
  touches the working web `Client`. Deferring it means temporary duplication. (Recommend:
  do it first.)
- **Native form-POST vs WebView login.** Confirm the antiforgery form-POST works cleanly
  across iOS/Android before committing; otherwise fall back to WebView.
- **Health Connect binding.** Does an acceptable .NET binding/plugin exist, or do we own a
  binding project? Resolve via an early spike (§9.6).
- **Exercise → Health activity-type map.** Ship a fixed default map, or expose per-exercise
  overrides in v1? (Recommend: fixed default first.)
- **Min OS versions** for iOS/Android (drives Health Connect availability + entitlement
  behavior).

---

## 11. References

- .NET MAUI: <https://learn.microsoft.com/dotnet/maui/>
- CommunityToolkit.Mvvm: <https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/>
- Apple HealthKit (workouts): <https://developer.apple.com/documentation/healthkit>
- .NET for iOS HealthKit bindings: <https://learn.microsoft.com/dotnet/api/healthkit>
- Android Health Connect: <https://developer.android.com/health-and-fitness/guides/health-connect>
- Health Connect data types (Exercise): <https://developer.android.com/reference/androidx/health/connect/client/records/ExerciseSessionRecord>
- MAUI SecureStorage: <https://learn.microsoft.com/dotnet/maui/platform-integration/storage/secure-storage>
