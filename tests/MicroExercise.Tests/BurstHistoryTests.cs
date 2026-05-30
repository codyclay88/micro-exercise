using MicroExercise.Core;
using MicroExercise.Core.Dtos;
using MicroExercise.Core.Entities;
using MicroExercise.Infrastructure.Services;

namespace MicroExercise.Tests;

public class BurstHistoryTests
{
    private const int UserId = AppDefaults.DemoUserId;
    private static readonly DateTimeOffset Anchor = new(2026, 5, 10, 9, 0, 0, TimeSpan.Zero);

    private static async Task<int> AddPoolEntryAsync(TestDb db, int userId, int exerciseTypeId = 1)
    {
        var entry = new ExercisePool { UserId = userId, ExerciseTypeId = exerciseTypeId, TargetQuantity = 10 };
        db.Context.ExercisePool.Add(entry);
        await db.Context.SaveChangesAsync();
        return entry.Id;
    }

    private static async Task<long> AddLogAsync(TestDb db, int poolId, int qty, DateTimeOffset ts)
    {
        var log = new WorkoutLog { ExercisePoolId = poolId, CompletedQuantity = qty, Timestamp = ts };
        db.Context.WorkoutLogs.Add(log);
        await db.Context.SaveChangesAsync();
        return log.Id;
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsOwnedBurstsInRange_MostRecentFirst()
    {
        using var db = new TestDb();
        var poolId = await AddPoolEntryAsync(db, UserId);
        await AddLogAsync(db, poolId, 10, Anchor);
        await AddLogAsync(db, poolId, 12, Anchor.AddHours(3));
        await AddLogAsync(db, poolId, 99, Anchor.AddDays(40)); // out of range

        var sut = new LogService(db.Context);
        var history = await sut.GetHistoryAsync(UserId, Anchor.AddDays(-1), Anchor.AddDays(1));

        Assert.Equal(2, history.Count);
        Assert.Equal(12, history[0].Quantity); // most recent first
        Assert.Equal(10, history[1].Quantity);
        Assert.Equal("Push-ups", history[0].ExerciseName);
    }

    [Fact]
    public async Task GetHistoryAsync_ExcludesOtherUsersBursts()
    {
        using var db = new TestDb();
        db.Context.Users.Add(new User { Id = 2, Email = "other@x.local", DisplayName = "Other" });
        await db.Context.SaveChangesAsync();
        var mine = await AddPoolEntryAsync(db, UserId);
        var theirs = await AddPoolEntryAsync(db, 2);
        await AddLogAsync(db, mine, 10, Anchor);
        await AddLogAsync(db, theirs, 50, Anchor);

        var sut = new LogService(db.Context);
        var history = await sut.GetHistoryAsync(UserId, Anchor.AddDays(-1), Anchor.AddDays(1));

        Assert.Single(history);
        Assert.Equal(10, history[0].Quantity);
    }

    [Fact]
    public async Task UpdateLogAsync_OwnedBurst_ChangesQuantityAndTimestamp()
    {
        using var db = new TestDb();
        var poolId = await AddPoolEntryAsync(db, UserId);
        var logId = await AddLogAsync(db, poolId, 10, Anchor);
        var newTs = Anchor.AddHours(-2);

        var sut = new LogService(db.Context);
        var updated = await sut.UpdateLogAsync(UserId, logId, new UpdateLogRequest(18, newTs));

        Assert.NotNull(updated);
        Assert.Equal(18, updated!.Quantity);
        Assert.Equal(newTs, updated.Timestamp);

        await using var verify = db.NewContext();
        var row = verify.WorkoutLogs.Single(l => l.Id == logId);
        Assert.Equal(18, row.CompletedQuantity);
        Assert.Equal(newTs, row.Timestamp);
    }

    [Fact]
    public async Task UpdateLogAsync_BurstOwnedByAnotherUser_ReturnsNull_AndDoesNotChange()
    {
        using var db = new TestDb();
        db.Context.Users.Add(new User { Id = 2, Email = "other@x.local", DisplayName = "Other" });
        await db.Context.SaveChangesAsync();
        var theirPool = await AddPoolEntryAsync(db, 2);
        var logId = await AddLogAsync(db, theirPool, 10, Anchor);

        var sut = new LogService(db.Context);
        var updated = await sut.UpdateLogAsync(UserId, logId, new UpdateLogRequest(99, Anchor));

        Assert.Null(updated);
        await using var verify = db.NewContext();
        Assert.Equal(10, verify.WorkoutLogs.Single(l => l.Id == logId).CompletedQuantity);
    }

    [Fact]
    public async Task DeleteLogAsync_OwnedBurst_RemovesIt()
    {
        using var db = new TestDb();
        var poolId = await AddPoolEntryAsync(db, UserId);
        var logId = await AddLogAsync(db, poolId, 10, Anchor);

        var sut = new LogService(db.Context);
        var deleted = await sut.DeleteLogAsync(UserId, logId);

        Assert.True(deleted);
        await using var verify = db.NewContext();
        Assert.Empty(verify.WorkoutLogs);
    }

    [Fact]
    public async Task DeleteLogAsync_BurstOwnedByAnotherUser_ReturnsFalse_AndKeepsRow()
    {
        using var db = new TestDb();
        db.Context.Users.Add(new User { Id = 2, Email = "other@x.local", DisplayName = "Other" });
        await db.Context.SaveChangesAsync();
        var theirPool = await AddPoolEntryAsync(db, 2);
        var logId = await AddLogAsync(db, theirPool, 10, Anchor);

        var sut = new LogService(db.Context);
        var deleted = await sut.DeleteLogAsync(UserId, logId);

        Assert.False(deleted);
        await using var verify = db.NewContext();
        Assert.Equal(1, verify.WorkoutLogs.Count());
    }
}
