# **Technical Specification: Micro-Burst Exercise Tracker (MVP)**

**Version:** 1.1  
**Target Architecture:** Single .NET Solution — ASP.NET Core + Blazor Web App (Unified)

> **Revision 1.1 note:** The frontend has been changed from an Angular SPA to a **Blazor Web App**, allowing the entire application (UI, API, and data access) to be hosted from a single .NET solution. This removes the separate Angular toolchain and CORS configuration, enables sharing C# models/DTOs across the UI and backend, and simplifies cookie-based authentication since the app is served same-origin. The backend feature set, database schema, and REST contract are otherwise unchanged.

## **1\. Overview & Core Philosophy**

The Micro-Burst Exercise Tracker is designed specifically for desk workers, remote employees, and individuals practicing "exercise snacking" or "Greasing the Groove" (GTG). The fundamental philosophy of this application is **low-friction, accurate logging**. Recording a 2-to-5-minute burst of exercise must take only a moment — a brief rest between sets — so users can capture the *true* amount they performed (reps-to-failure) and immediately return to their work tasks. The aim is to encourage training to failure, not to optimize for the largest number of fixed-size sets.

## **2\. Tech Stack Architecture**

* **Frontend:** **Blazor WebAssembly** SPA (`MicroExercise.Client`), served as static files by the ASP.NET Core host and talking to the REST API (§5) over `HttpClient` with cookie auth. Running the UI in the browser keeps the server **stateless** (no SignalR circuit to hold per-user in RAM), which scales on small/cheap infrastructure and is resilient to flaky mobile connections; after the initial load, one-click logging and the inline steppers are local with no per-interaction round-trip. Responsive layout via **Bootstrap 5**. The UI must optimize for both mobile web viewports and pinned/split-screen desktop browsers. *(Sign-in/registration are server-rendered (static SSR) so the auth cookie is written on a real HTTP request.)*  
* **Backend:** ASP.NET Core (**.NET 10**, the current LTS), optimized for high-performance, low-latency transactional logging. The REST API (§5) is exposed via Minimal APIs and is consumed by the Blazor UI as well as any future clients.  
* **Database:** **PostgreSQL** via Entity Framework Core (Npgsql provider) in both development and production, for dev/prod parity. Local development runs Postgres in a Docker container (`compose.dev.yaml`); production runs it alongside the app on a single host (`compose.yaml`). The EF Core migration set is Npgsql-specific. The automated test suite uses an in-memory SQLite database (schema created from the model via `EnsureCreated`, which is provider-agnostic), so tests need no running database.  
* **Authentication:** **ASP.NET Core Identity** with secure, HttpOnly, SameSite cookie authentication for seamless, long-lived desktop sessions without client-side token refresh overhead. Users register and sign in via themed pages (`/register`, `/login`); Identity uses integer keys to match the rest of the schema (`AspNetUsers` etc.). New registrations receive a few starter exercises. Email confirmation is disabled in the MVP (no mail service yet); all application pages and API endpoints require authorization.

## **3\. Database Schema Design**

The application relies on a compact, highly normalized four-table structure to manage users, exercise configurations, and historical log data.

| Table Name | Column Name | Data Type | Constraints / Description   |
| :---- | :---- | :---- | :---- |
| **ExerciseType** (System Lookups) | Id | INT | Primary Key, Identity |
|  | Name | VARCHAR(50) | e.g., "Push-ups", "Dead Hang", "KB Swings" |
|  | DefaultTrackingType | VARCHAR(20) | Enum value: Reps or Seconds |
|  | OwnerUserId | INT (nullable) | FK \-\> User(Id); null \= global catalog, set \= a user's private custom exercise |
| **ExercisePool** (User Customizations) | Id | INT | Primary Key, Identity |
|  | UserId | INT | Foreign Key \-\> User(Id) |
|  | ExerciseTypeId | INT | Foreign Key \-\> ExerciseType(Id) |
|  | CustomName | VARCHAR(100) | Optional override (e.g., "KB RDL (35 lbs)") |
|  | LastQuantity | INT | The most recent burst's amount; pre-fills the log dialog (your next burst is usually similar to your last). Seeded at creation, overwritten on each log; overtyped per set (e.g., 10\) |
|  | IsActive | BOOLEAN | Soft-delete flag to protect historical data integrity |
|  | SortOrder | INT | Ascending display priority for the dashboard grid (§4.2 prioritization) |
| **WorkoutLog** (Transactional Data) | Id | BIGINT | Primary Key, Identity |
|  | ExercisePoolId | INT | Foreign Key \-\> ExercisePool(Id) |
|  | Timestamp | DATETIMEOFFSET | Crucial for maintaining accurate cross-timezone dates |
|  | CompletedQuantity | INT | The precise reps or seconds performed in that single burst |
|  | ResistanceType | VARCHAR(20) | How the burst's optional added resistance is expressed: `Bodyweight` (default, no added load), `Weight`, or `Band` |
|  | ResistanceAmount | NUMERIC(6,2) (nullable) | Numeric load for a `Weight` burst (paired with WeightUnit); null otherwise |
|  | WeightUnit | VARCHAR(20) (nullable) | Unit for ResistanceAmount: `Pounds` or `Kilograms`; null unless `Weight` |
|  | BandLabel | VARCHAR(40) (nullable) | Free-text colour/label for a `Band` burst (e.g. "Green"); null otherwise |

*Note: A standard User table managing minimal credentials and metadata completes the core operational dataset.*

## **4\. Functional Feature Specifications**

### **4.1 The Log Dashboard (UX Core)**

The primary view of the application must prioritize fast, accurate data entry.

* **Grid Presentation:** Renders the user’s active ExercisePool items as "Quick-Log Cards," each showing the exercise and today's running totals.  
* **Primary Action:** A card's **Log** button opens a shared burst-log dialog pre-targeted to that exercise. The user **free-types the amount actually performed** — the field is pre-filled with the exercise's `LastQuantity` — the amount you did on your most recent burst — as a convenient default, but it is meant to be overtyped: a set trained *to failure* rarely lands on a round number, and stepping `+1` forty-three times to reach 53 is hostile. This deliberately favors recording true reps-to-failure over racing to log fixed-size sets.  
* **Shared, ubiquitous dialog:** The same dialog is reachable from anywhere via a global "＋ Log" action and includes an **exercise picker**, so a burst can be recorded without first navigating to the dashboard (e.g. straight from the Goals screen). Logging from any screen advances the relevant card totals and goal progress live.
* **Create-while-logging:** The picker includes a "**＋ New exercise…**" option so a brand-new movement can be created *in the act of logging* — the user supplies a name and whether it's measured in Reps or Seconds, and the burst's quantity seeds the new exercise's target. This avoids the ceremony of registering an exercise in the pool before the first burst can be recorded (a common case: picking up a new movement and logging a quick set immediately). The new exercise persists as a reusable, user-owned custom exercise and a pool card, created via `POST /api/exercises/custom` before the burst is logged. When the pool is empty, the dialog opens straight into this mode.
* **Optional resistance:** Each burst can record an optional resistance, defaulting to **Bodyweight** (no added load). For weighted movements the user enters a numeric amount plus a unit (lbs/kg); for resistance bands — whose load isn't reliably known — they enter a free-text colour/label (e.g. "Green"). Resistance is per-burst (not stored on the pool item) and shown alongside the amount in History.

### **4.2 Pool Management**

An administrative configuration panel where users tailor their recurring rotation of movements.

* Allows discovery and addition of predefined global ExerciseType entries.  
* **Custom exercises:** users can create a brand-new exercise from scratch — choosing its name, tracking type (Reps or Seconds), and target — without it pre-existing in the global catalog (e.g. "Bunny Hops", 100 reps; "Planks", 30 seconds). A custom exercise is stored as a user-owned `ExerciseType` (`OwnerUserId` set) so it is private to its creator and reusable from their catalog for additional variants.  
* Supports full customization of the display text and the last-amount value.  
* Provides sorting or prioritization mechanics to determine dashboard layout hierarchy.

### **4.3 Date-Range Analytics & Reporting**

A history and aggregation module helping users quantify their distributed daily volume over time.

* **Filter Controls:** A simple date range selection component capturing a bounding FromDate and ToDate.  
* **Aggregation Logic:** Groups transactional data sets by ExercisePoolId, executing a SUM(CompletedQuantity) and a COUNT() across records matching the temporal constraints.  
* **Visual Output:** Clean, tabular reports outlining summary achievements (e.g., *"May 1 \- May 7: Accumulated 450 Push-ups, 120 Pull-ups, and 90 seconds of cumulative Dead Hangs"*).

### **4.4 Burst History & Editing**

Because logging is quick and free-typed, accidental or imprecise entries are inevitable. Users must be able to review and correct their recorded bursts after the fact.

* **History View:** A dedicated `/history` page lists individual `WorkoutLog` bursts within a selected date range, most recent first, showing when each burst occurred, the exercise, the amount, and any non-bodyweight resistance (e.g. "15 lbs", "Green band").  
* **Edit:** A burst's quantity *and* timestamp can be corrected in place (inline), e.g. fixing a fat-fingered rep count or the time of day.  
* **Delete:** A burst can be permanently removed (hard delete, with an inline confirm). Unlike `ExercisePool` soft-deletes, transactional bursts are genuinely discarded — an accidental entry is something the user wants gone, and this keeps report aggregation filter-free.  
* **Ownership:** All edit/delete operations are scoped to the owning user.

## **5\. API Endpoint Contract**

The REST contract below is exposed via ASP.NET Core Minimal APIs and is the data path for the Blazor WebAssembly UI (over `HttpClient` with the auth cookie), as well as any external/future clients. Endpoints derive the current user from the cookie via `ICurrentUser`; the client never passes a user id. (Auth helper: `GET /api/auth/me` returns the signed-in user so the SPA can establish its auth state.)  
GET  /api/exercises/pool  
Returns: Collection of active configured exercises for the logged-in user's quick-log grid.

POST /api/exercises/pool  
Body: { "exerciseTypeId": int, "lastQuantity": int, "customName": string }  
Returns: The newly instantiated ExercisePool entity object.

POST /api/exercises/custom  
Body: { "name": string, "trackingType": "Reps"|"Seconds", "lastQuantity": int }  
Returns: The new pool item; a user-owned ExerciseType is created and added to the pool.

POST /api/logs  
Body: { "exercisePoolId": int, "quantity": int, "resistance"?: Resistance }  
Returns: HTTP 201 Created confirmation of written workout log (echoes the resolved resistance).

  Resistance (optional; omit/null = bodyweight) is one of:
    { "type": "Bodyweight" }
    { "type": "Weight", "amount": 15, "unit": "Pounds"|"Kilograms" }
    { "type": "Band", "bandLabel": "Green" }
  Incomplete shapes (Weight with no amount, Band with a blank label) normalize to Bodyweight.

GET  /api/logs?from=YYYY-MM-DD\&to=YYYY-MM-DD  
Returns: Array of individual bursts in range (most recent first) for the history view.

PUT  /api/logs/{id}  
Body: { "quantity": int, "timestamp": DateTimeOffset, "resistance"?: Resistance }  
Returns: The corrected burst, or 404 if not found / not owned by the user. Pass the burst's
existing resistance to preserve it (the inline editors only change quantity + timestamp); a
null/omitted resistance records bodyweight.

DELETE /api/logs/{id}  
Returns: HTTP 204 No Content, or 404 if not found / not owned by the user.

GET  /api/reports/summary?from=YYYY-MM-DD\&to=YYYY-MM-DD  
Returns: Array of aggregated objects tracking total volume and count per pool item.

GET  /api/goals?includeCompleted=true|false  
Returns: The user's goals with live, computed progress (current/target/percent/remaining) and status (Active|Achieved|Expired), newest first. `includeCompleted=false` returns only Active goals.

GET  /api/goals/{id}  
Returns: A single goal with progress, or 404 if not found / not owned by the user.

POST /api/goals  
Body: { "exercisePoolId": int, "targetQuantity": int, "deadline": DateTimeOffset, "startDate": DateTimeOffset|null }  
Returns: HTTP 201 with the created goal (startDate defaults to "now" when null), 400 on invalid target/deadline, or 404 if the pool item isn't owned/active.

DELETE /api/goals/{id}  
Returns: HTTP 204 No Content (hard delete; burst history is unaffected), or 404 if not found / not owned.

### **5.1 Backend LINQ Aggregation Pattern**

Below is the declarative LINQ syntax pattern executed by the repository layer to calculate historical summary reports:  
public async Task\<List\<ExerciseSummaryDto\>\> GetSummaryAsync(int userId, DateTimeOffset start, DateTimeOffset end)  
{  
    return await \_context.WorkoutLogs  
        .Where(l \=\> l.ExercisePool.UserId \== userId && l.Timestamp \>= start && l.Timestamp \<= end)  
        .GroupBy(l \=\> new { l.ExercisePoolId, l.ExercisePool.CustomName, l.ExercisePool.ExerciseType.DefaultTrackingType })  
        .Select(g \=\> new ExerciseSummaryDto  
        {  
            ExerciseName \= g.Key.CustomName,  
            TrackingType \= g.Key.DefaultTrackingType,  
            TotalVolume \= g.Sum(x \=\> x.CompletedQuantity),  
            TotalBursts \= g.Count()  
        })  
        .ToListAsync();  
}

## **6\. Future Roadmaps & Enhancements**

1. **Integrated Desktop Interval Timers:** Visual workspace Pomodoro clock components triggering subtle UI animations or sound notifications when a work interval expires, calling for the next exercise snack.

*(Keyboard navigation hotkeys, formerly listed here, shipped as §4.5.)*