using MicroExercise.Core.Abstractions;
using MicroExercise.Core.Dtos;

namespace MicroExercise.Web.Endpoints;

/// <summary>
/// Maps the REST contract from spec §5. These endpoints serve external/future clients;
/// the Blazor UI calls the same Core services directly for lowest latency.
/// </summary>
public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        // GET /api/exercises/pool — active quick-log grid for the current user.
        api.MapGet("/exercises/pool", async (
            IPoolService pool, ICurrentUser user, CancellationToken ct) =>
        {
            var items = await pool.GetActivePoolAsync(user.UserId, ct);
            return Results.Ok(items);
        });

        // GET /api/exercises/types — global catalog for pool discovery (spec §4.2).
        api.MapGet("/exercises/types", async (
            IPoolService pool, CancellationToken ct) =>
        {
            var types = await pool.GetExerciseTypesAsync(ct);
            return Results.Ok(types);
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
