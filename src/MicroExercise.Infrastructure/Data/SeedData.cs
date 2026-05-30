using MicroExercise.Core;
using MicroExercise.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroExercise.Infrastructure.Data;

/// <summary>
/// Idempotent runtime seeding of user-scoped sample data (pool entries + a little
/// history) so the dashboard and reports are populated on first run. Global lookups
/// and the demo user itself are seeded through migrations (<c>HasData</c>).
/// </summary>
public static class SeedData
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        // Only seed the demo user's pool the first time — never clobber real data.
        var alreadySeeded = await db.ExercisePool
            .AnyAsync(p => p.UserId == AppDefaults.DemoUserId, ct);
        if (alreadySeeded)
            return;

        var pool = new[]
        {
            new ExercisePool { UserId = AppDefaults.DemoUserId, ExerciseTypeId = 1, TargetQuantity = 10, SortOrder = 0 }, // Push-ups
            new ExercisePool { UserId = AppDefaults.DemoUserId, ExerciseTypeId = 2, TargetQuantity = 5,  SortOrder = 1 }, // Pull-ups
            new ExercisePool { UserId = AppDefaults.DemoUserId, ExerciseTypeId = 3, TargetQuantity = 20, SortOrder = 2 }, // Squats
            new ExercisePool { UserId = AppDefaults.DemoUserId, ExerciseTypeId = 6, TargetQuantity = 30, SortOrder = 3 }, // Dead Hang (sec)
        };
        db.ExercisePool.AddRange(pool);
        await db.SaveChangesAsync(ct);

        // A fortnight of sample bursts so date-range reports have something to show.
        var today = DateTimeOffset.Now;
        var logs = new List<WorkoutLog>();
        for (var dayOffset = 13; dayOffset >= 0; dayOffset--)
        {
            var day = today.AddDays(-dayOffset);
            foreach (var entry in pool)
            {
                // Two bursts per active day, at the configured target.
                logs.Add(new WorkoutLog
                {
                    ExercisePoolId = entry.Id,
                    CompletedQuantity = entry.TargetQuantity,
                    Timestamp = day.Date.AddHours(10)
                });
                logs.Add(new WorkoutLog
                {
                    ExercisePoolId = entry.Id,
                    CompletedQuantity = entry.TargetQuantity,
                    Timestamp = day.Date.AddHours(15)
                });
            }
        }
        db.WorkoutLogs.AddRange(logs);
        await db.SaveChangesAsync(ct);
    }
}
