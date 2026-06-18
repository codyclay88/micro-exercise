using MicroExercise.Core;
using MicroExercise.Core.Entities;
using MicroExercise.Core.Enums;
using MicroExercise.Infrastructure.Services;

namespace MicroExercise.Tests;

public class ReportServiceTests
{
    private const int UserId = TestDb.PrimaryUserId;

    [Fact]
    public async Task GetSummaryAsync_SumsVolumeAndCountsBurstsWithinRange()
    {
        using var db = new TestDb();
        var pool = new ExercisePool { UserId = UserId, ExerciseTypeId = 1, LastQuantity = 10, CustomName = null };
        db.Context.ExercisePool.Add(pool);
        await db.Context.SaveChangesAsync();

        var anchor = new DateTimeOffset(2026, 5, 10, 9, 0, 0, TimeSpan.Zero);
        db.Context.WorkoutLogs.AddRange(
            new WorkoutLog { ExercisePoolId = pool.Id, CompletedQuantity = 10, Timestamp = anchor },
            new WorkoutLog { ExercisePoolId = pool.Id, CompletedQuantity = 12, Timestamp = anchor.AddHours(5) },
            new WorkoutLog { ExercisePoolId = pool.Id, CompletedQuantity = 8, Timestamp = anchor.AddDays(2) },
            // Outside the window — must be excluded.
            new WorkoutLog { ExercisePoolId = pool.Id, CompletedQuantity = 99, Timestamp = anchor.AddDays(30) });
        await db.Context.SaveChangesAsync();

        var sut = new ReportService(db.Context);
        var summary = await sut.GetSummaryAsync(
            UserId,
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 15, 23, 59, 59, TimeSpan.Zero));

        var row = Assert.Single(summary);
        Assert.Equal(pool.Id, row.ExercisePoolId);
        Assert.Equal("Push-ups", row.ExerciseName);
        Assert.Equal(TrackingType.Reps, row.TrackingType);
        Assert.Equal(30, row.TotalVolume); // 10 + 12 + 8
        Assert.Equal(3, row.TotalBursts);  // the 99 is out of range
    }

    [Fact]
    public async Task GetSummaryAsync_GroupsPerPoolItem_AndUsesCustomName()
    {
        using var db = new TestDb();
        var pushups = new ExercisePool { UserId = UserId, ExerciseTypeId = 1, LastQuantity = 10 };
        var hang = new ExercisePool { UserId = UserId, ExerciseTypeId = 6, LastQuantity = 30, CustomName = "Bar Hang" };
        db.Context.ExercisePool.AddRange(pushups, hang);
        await db.Context.SaveChangesAsync();

        var t = new DateTimeOffset(2026, 5, 10, 9, 0, 0, TimeSpan.Zero);
        db.Context.WorkoutLogs.AddRange(
            new WorkoutLog { ExercisePoolId = pushups.Id, CompletedQuantity = 10, Timestamp = t },
            new WorkoutLog { ExercisePoolId = hang.Id, CompletedQuantity = 30, Timestamp = t },
            new WorkoutLog { ExercisePoolId = hang.Id, CompletedQuantity = 45, Timestamp = t.AddHours(1) });
        await db.Context.SaveChangesAsync();

        var sut = new ReportService(db.Context);
        var summary = await sut.GetSummaryAsync(
            UserId,
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 31, 23, 59, 59, TimeSpan.Zero));

        Assert.Equal(2, summary.Count);

        var hangRow = Assert.Single(summary, s => s.ExercisePoolId == hang.Id);
        Assert.Equal("Bar Hang", hangRow.ExerciseName);
        Assert.Equal(TrackingType.Seconds, hangRow.TrackingType);
        Assert.Equal(75, hangRow.TotalVolume);
        Assert.Equal(2, hangRow.TotalBursts);
    }

    [Fact]
    public async Task GetSummaryAsync_NoLogs_ReturnsEmpty()
    {
        using var db = new TestDb();
        var sut = new ReportService(db.Context);

        var summary = await sut.GetSummaryAsync(
            UserId,
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 31, 23, 59, 59, TimeSpan.Zero));

        Assert.Empty(summary);
    }
}
