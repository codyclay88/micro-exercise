# **Technical Specification: Micro-Burst Exercise Tracker (MVP)**

**Version:** 1.1  
**Target Architecture:** Single .NET Solution — ASP.NET Core + Blazor Web App (Unified)

> **Revision 1.1 note:** The frontend has been changed from an Angular SPA to a **Blazor Web App**, allowing the entire application (UI, API, and data access) to be hosted from a single .NET solution. This removes the separate Angular toolchain and CORS configuration, enables sharing C# models/DTOs across the UI and backend, and simplifies cookie-based authentication since the app is served same-origin. The backend feature set, database schema, and REST contract are otherwise unchanged.

## **1\. Overview & Core Philosophy**

The Micro-Burst Exercise Tracker is designed specifically for desk workers, remote employees, and individuals practicing "exercise snacking" or "Greasing the Groove" (GTG). The fundamental philosophy of this application is **zero friction**. Logging a 2-to-5-minute burst of exercise must require minimal effort, minimal clicks, and zero interface lag so users can immediately return to their work tasks.

## **2\. Tech Stack Architecture**

* **Frontend:** **Blazor Web App** (unified .NET 10 template) using the **Interactive Server** render mode by default. UI events round-trip over SignalR, eliminating client-side API serialization and delivering the low-latency "one-click" feel the philosophy demands; individual components may opt into WebAssembly later if needed. Responsive layout via **Bootstrap 5** (ships with the template, no separate JS build step). The UI must optimize for both mobile web viewports and pinned/split-screen desktop browsers.  
* **Backend:** ASP.NET Core (**.NET 10**, the current LTS), optimized for high-performance, low-latency transactional logging. The REST API (§5) is exposed via Minimal APIs and is consumed by the Blazor UI as well as any future clients.  
* **Database:** **SQLite** via Entity Framework Core for the MVP (file-based, zero-install local development). The provider is swappable to SQL Server / PostgreSQL with a single configuration change, as EF Core migrations remain provider-agnostic.  
* **Authentication:** **ASP.NET Core Identity** with secure, HttpOnly, SameSite cookie authentication for seamless, long-lived desktop sessions without client-side token refresh overhead. Users register and sign in via themed pages (`/register`, `/login`); Identity uses integer keys to match the rest of the schema (`AspNetUsers` etc.). New registrations receive a few starter exercises. Email confirmation is disabled in the MVP (no mail service yet); all application pages and API endpoints require authorization.

## **3\. Database Schema Design**

The application relies on a compact, highly normalized four-table structure to manage users, exercise configurations, and historical log data.

| Table Name | Column Name | Data Type | Constraints / Description   |
| :---- | :---- | :---- | :---- |
| **ExerciseType** (System Lookups) | Id | INT | Primary Key, Identity |
|  | Name | VARCHAR(50) | e.g., "Push-ups", "Dead Hang", "KB Swings" |
|  | DefaultTrackingType | VARCHAR(20) | Enum value: Reps or Seconds |
| **ExercisePool** (User Customizations) | Id | INT | Primary Key, Identity |
|  | UserId | INT | Foreign Key \-\> User(Id) |
|  | ExerciseTypeId | INT | Foreign Key \-\> ExerciseType(Id) |
|  | CustomName | VARCHAR(100) | Optional override (e.g., "KB RDL (35 lbs)") |
|  | TargetQuantity | INT | Standard baseline burst volume per set (e.g., 10\) |
|  | IsActive | BOOLEAN | Soft-delete flag to protect historical data integrity |
|  | SortOrder | INT | Ascending display priority for the dashboard grid (§4.2 prioritization) |
| **WorkoutLog** (Transactional Data) | Id | BIGINT | Primary Key, Identity |
|  | ExercisePoolId | INT | Foreign Key \-\> ExercisePool(Id) |
|  | Timestamp | DATETIMEOFFSET | Crucial for maintaining accurate cross-timezone dates |
|  | CompletedQuantity | INT | The precise reps or seconds performed in that single burst |

*Note: A standard User table managing minimal credentials and metadata completes the core operational dataset.*

## **4\. Functional Feature Specifications**

### **4.1 The "One-Click Log" Dashboard (UX Core)**

The primary view of the application must prioritize immediate data entry.

* **Grid Presentation:** Renders the user’s active ExercisePool items as actionable "Quick-Log Cards."  
* **Primary Action:** Tapping or clicking the primary surface of the card immediately dispatches a POST request to write a log entry matching the pre-configured TargetQuantity.  
* **Inline Modifiers:** Small \+ and \- stepper buttons must flank the main interaction zone, allowing micro-adjustments (e.g., logging 12 reps instead of 10\) completely inline without launching distinct modal windows.

### **4.2 Pool Management**

An administrative configuration panel where users tailor their recurring rotation of movements.

* Allows discovery and addition of predefined global ExerciseType entries.  
* Supports full customization of the target display text and target unit quantities.  
* Provides sorting or prioritization mechanics to determine dashboard layout hierarchy.

### **4.3 Date-Range Analytics & Reporting**

A history and aggregation module helping users quantify their distributed daily volume over time.

* **Filter Controls:** A simple date range selection component capturing a bounding FromDate and ToDate.  
* **Aggregation Logic:** Groups transactional data sets by ExercisePoolId, executing a SUM(CompletedQuantity) and a COUNT() across records matching the temporal constraints.  
* **Visual Output:** Clean, tabular reports outlining summary achievements (e.g., *"May 1 \- May 7: Accumulated 450 Push-ups, 120 Pull-ups, and 90 seconds of cumulative Dead Hangs"*).

### **4.4 Burst History & Editing**

Because logging is one tap, accidental or imprecise entries are inevitable. Users must be able to review and correct their recorded bursts after the fact.

* **History View:** A dedicated `/history` page lists individual `WorkoutLog` bursts within a selected date range, most recent first, showing when each burst occurred, the exercise, and the amount.  
* **Edit:** A burst's quantity *and* timestamp can be corrected in place (inline), e.g. fixing a fat-fingered rep count or the time of day.  
* **Delete:** A burst can be permanently removed (hard delete, with an inline confirm). Unlike `ExercisePool` soft-deletes, transactional bursts are genuinely discarded — an accidental entry is something the user wants gone, and this keeps report aggregation filter-free.  
* **Ownership:** All edit/delete operations are scoped to the owning user.

## **5\. API Endpoint Contract**

The REST contract below remains explicitly decoupled and is exposed via ASP.NET Core Minimal APIs. The Blazor Interactive Server UI may invoke the underlying services directly for lowest latency, while these endpoints serve any external or future clients.  
GET  /api/exercises/pool  
Returns: Collection of active configured exercises for the logged-in user's quick-log grid.

POST /api/exercises/pool  
Body: { "exerciseTypeId": int, "targetQuantity": int, "customName": string }  
Returns: The newly instantiated ExercisePool entity object.

POST /api/logs  
Body: { "exercisePoolId": int, "quantity": int }  
Returns: HTTP 201 Created confirmation of written workout log.

GET  /api/logs?from=YYYY-MM-DD\&to=YYYY-MM-DD  
Returns: Array of individual bursts in range (most recent first) for the history view.

PUT  /api/logs/{id}  
Body: { "quantity": int, "timestamp": DateTimeOffset }  
Returns: The corrected burst, or 404 if not found / not owned by the user.

DELETE /api/logs/{id}  
Returns: HTTP 204 No Content, or 404 if not found / not owned by the user.

GET  /api/reports/summary?from=YYYY-MM-DD\&to=YYYY-MM-DD  
Returns: Array of aggregated objects tracking total volume and count per pool item.

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
2. **Keyboard Navigation Hotkeys:** Direct bindings matching row indices or numbers 1-6 on full desktop keypads, granting users the ability to log an exercise block entirely mouse-free.