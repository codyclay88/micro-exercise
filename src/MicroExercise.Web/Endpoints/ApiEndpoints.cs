using MicroExercise.Core.Abstractions;
using MicroExercise.Core.Dtos;
using MicroExercise.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace MicroExercise.Web.Endpoints;

/// <summary>
/// Maps the REST contract from spec §5. These endpoints serve external/future clients;
/// the Blazor UI calls the same Core services directly for lowest latency.
/// </summary>
public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        // Antiforgery is disabled on the API: it's a same-origin JSON API consumed by the WASM
        // client over cookie auth (HttpOnly, SameSite=Lax). The login/register SSR forms keep
        // antiforgery. (CSRF posture: SameSite + JSON content-type.)
        var api = app.MapGroup("/api").RequireAuthorization().DisableAntiforgery();

        // GET /api/auth/me — the current user's identity, so the WASM client can build its
        // ClaimsPrincipal (the auth cookie is HttpOnly and unreadable from JS). 401 if signed out.
        api.MapGet("/auth/me", async (
            ICurrentUser user, UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            var appUser = await users.FindByIdAsync(user.UserId.ToString());
            return appUser is null
                ? Results.Unauthorized()
                : Results.Ok(new CurrentUserDto(
                    appUser.Id, appUser.DisplayName ?? appUser.UserName ?? "", appUser.Email));
        });

        // GET /api/exercises/pool — active quick-log grid for the current user.
        api.MapGet("/exercises/pool", async (
            IPoolService pool, ICurrentUser user, CancellationToken ct) =>
        {
            var items = await pool.GetActivePoolAsync(user.UserId, ct);
            return Results.Ok(items);
        });

        // GET /api/exercises/types — catalog for pool discovery: global + the user's custom (spec §4.2).
        api.MapGet("/exercises/types", async (
            IPoolService pool, ICurrentUser user, CancellationToken ct) =>
        {
            var types = await pool.GetExerciseTypesAsync(user.UserId, ct);
            return Results.Ok(types);
        });

        // POST /api/exercises/custom — create a brand-new custom exercise and add it to the pool.
        api.MapPost("/exercises/custom", async (
            CreateCustomExerciseRequest request, IPoolService pool, ICurrentUser user, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.Name))
                errors["name"] = ["Exercise name is required."];
            if (request.TargetQuantity <= 0)
                errors["targetQuantity"] = ["Target quantity must be greater than zero."];
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var created = await pool.AddCustomExerciseAsync(user.UserId, request, ct);
            return Results.Created($"/api/exercises/pool/{created.Id}", created);
        });

        // POST /api/exercises/pool — add an exercise to the user's pool.
        api.MapPost("/exercises/pool", async (
            CreatePoolItemRequest request, IPoolService pool, ICurrentUser user, CancellationToken ct) =>
        {
            if (request.TargetQuantity <= 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["targetQuantity"] = ["Target quantity must be greater than zero."]
                });

            try
            {
                var created = await pool.AddPoolItemAsync(user.UserId, request, ct);
                return Results.Created($"/api/exercises/pool/{created.Id}", created);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["exerciseTypeId"] = [ex.Message]
                });
            }
        });

        // PUT /api/exercises/pool/{id} — customize a pool entry's name/target.
        api.MapPut("/exercises/pool/{id:int}", async (
            int id, UpdatePoolItemRequest request, IPoolService pool, ICurrentUser user, CancellationToken ct) =>
        {
            if (request.TargetQuantity <= 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["targetQuantity"] = ["Target quantity must be greater than zero."]
                });

            var updated = await pool.UpdatePoolItemAsync(user.UserId, id, request, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        // POST /api/exercises/pool/{id}/move?up=true|false — reorder the dashboard grid.
        api.MapPost("/exercises/pool/{id:int}/move", async (
            int id, bool up, IPoolService pool, ICurrentUser user, CancellationToken ct) =>
        {
            var moved = await pool.MovePoolItemAsync(user.UserId, id, up, ct);
            return moved ? Results.NoContent() : Results.NotFound();
        });

        // DELETE /api/exercises/pool/{id} — soft-delete (preserves history).
        api.MapDelete("/exercises/pool/{id:int}", async (
            int id, IPoolService pool, ICurrentUser user, CancellationToken ct) =>
        {
            var removed = await pool.DeactivatePoolItemAsync(user.UserId, id, ct);
            return removed ? Results.NoContent() : Results.NotFound();
        });

        // POST /api/logs — record a single burst against an owned pool item.
        api.MapPost("/logs", async (
            CreateLogRequest request, ILogService logs, ICurrentUser user, CancellationToken ct) =>
        {
            if (request.Quantity <= 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["quantity"] = ["Quantity must be greater than zero."]
                });

            var result = await logs.LogAsync(user.UserId, request, ct);
            return result is null
                ? Results.NotFound()
                : Results.Created($"/api/logs/{result.Id}", result);
        });

        // GET /api/logs?from=YYYY-MM-DD&to=YYYY-MM-DD — individual bursts for the history view.
        api.MapGet("/logs", async (
            DateOnly from, DateOnly to, ILogService logs, ICurrentUser user, CancellationToken ct) =>
        {
            if (to < from)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["to"] = ["'to' must be on or after 'from'."]
                });

            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue));
            var end = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue));
            var history = await logs.GetHistoryAsync(user.UserId, start, end, ct);
            return Results.Ok(history);
        });

        // PUT /api/logs/{id} — correct a burst's quantity and timestamp.
        api.MapPut("/logs/{id:long}", async (
            long id, UpdateLogRequest request, ILogService logs, ICurrentUser user, CancellationToken ct) =>
        {
            if (request.Quantity <= 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["quantity"] = ["Quantity must be greater than zero."]
                });

            var updated = await logs.UpdateLogAsync(user.UserId, id, request, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        // DELETE /api/logs/{id} — permanently remove a burst.
        api.MapDelete("/logs/{id:long}", async (
            long id, ILogService logs, ICurrentUser user, CancellationToken ct) =>
        {
            var deleted = await logs.DeleteLogAsync(user.UserId, id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        // GET /api/reports/summary?from=YYYY-MM-DD&to=YYYY-MM-DD — aggregated volume per item.
        api.MapGet("/reports/summary", async (
            DateOnly from, DateOnly to, IReportService reports, ICurrentUser user, CancellationToken ct) =>
        {
            if (to < from)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["to"] = ["'to' must be on or after 'from'."]
                });

            // Inclusive of the entire 'to' day, in the server's local offset.
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue));
            var end = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue));

            var summary = await reports.GetSummaryAsync(user.UserId, start, end, ct);
            return Results.Ok(summary);
        });

        return app;
    }
}
