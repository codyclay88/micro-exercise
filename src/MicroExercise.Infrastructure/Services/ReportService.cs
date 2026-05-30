using MicroExercise.Core.Abstractions;
using MicroExercise.Core.Dtos;
using MicroExercise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MicroExercise.Infrastructure.Services;

public class ReportService(AppDbContext db) : IReportService
{
    public async Task<IReadOnlyList<ExerciseSummaryDto>> GetSummaryAsync(
        int userId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
    {
        // Spec §5.1 aggregation pattern: filter by user + window, group per pool item,
        // then SUM(quantity) and COUNT(bursts). CustomName falls back to the type name.
        // The display name is resolved before grouping so the GROUP BY operates on flat
        // scalar columns (a multi-level navigation inside the key does not translate).
        return await db.WorkoutLogs
            .Where(l => l.ExercisePool!.UserId == userId
                        && l.Timestamp >= start
                        && l.Timestamp <= end)
            .Select(l => new
            {
                l.ExercisePoolId,
                Name = l.ExercisePool!.CustomName ?? l.ExercisePool.ExerciseType!.Name,
                l.ExercisePool!.ExerciseType!.DefaultTrackingType,
                l.CompletedQuantity
            })
            .GroupBy(x => new { x.ExercisePoolId, x.Name, x.DefaultTrackingType })
            .Select(g => new ExerciseSummaryDto(
                g.Key.ExercisePoolId,
                g.Key.Name,
                g.Key.DefaultTrackingType,
                g.Sum(x => (long)x.CompletedQuantity),
                g.Count()))
            .ToListAsync(ct);
    }
}
