using MicroExercise.Core;
using MicroExercise.Core.Dtos;
using MicroExercise.Core.Entities;
using MicroExercise.Core.Enums;
using MicroExercise.Infrastructure.Identity;
using MicroExercise.Infrastructure.Services;

namespace MicroExercise.Tests;

public class BurstHistoryTests
{
    private const int UserId = TestDb.PrimaryUserId;
    private static readonly DateTimeOffset Anchor = new(2026, 5, 10, 9, 0, 0, TimeSpan.Zero);

    private static async Task<int> AddPoolEntryAsync(TestDb db, int userId, int exerciseTypeId = 1)
    {
        var entry = new ExercisePool { UserId = userId, ExerciseTypeId = exerciseTypeId, LastQuantity = 10 };
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
        db.Context.Users.Add(new ApplicationUser { Id = 2, UserName = "other@x.local", Email = "other@x.local", DisplayName = "Other" });
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
    public async Task GetHistoryAsync_ProjectsResistance()
    {
        using var db = new TestDb();
        var poolId = await AddPoolEntryAsync(db, UserId);
        db.Context.WorkoutLogs.Add(new WorkoutLog
        {
            ExercisePoolId = poolId, CompletedQuantity = 10, Timestamp = Anchor,
            ResistanceType = ResistanceType.Weight, ResistanceAmount = 20m, WeightUnit = WeightUnit.Pounds
        });
        await db.Context.SaveChangesAsync();

        var sut = new LogService(db.Context);
        var history = await sut.GetHistoryAsync(UserId, Anchor.AddDays(-1), Anchor.AddDays(1));

        var burst = Assert.Single(history);
        Assert.Equal(ResistanceType.Weight, burst.Resistance.Type);
        Assert.Equal(20m, burst.Resistance.Amount);
        Assert.Equal(WeightUnit.Pounds, burst.Resistance.Unit);
        Assert.Equal("20 lbs", burst.Resistance.Describe());
    }

    [Fact]
    public async Task UpdateLogAsync_ChangesResistance()
    {
        using var db = new TestDb();
        var poolId = await AddPoolEntryAsync(db, UserId);
        var logId = await AddLogAsync(db, poolId, 10, Anchor);   // starts bodyweight

        var sut = new LogService(db.Context);
        var request = new UpdateLogRequest(10, Anchor, new ResistanceDto(ResistanceType.Band, BandLabel: "Red"));
        var updated = await sut.UpdateLogAsync(UserId, logId, request);

        Assert.NotNull(updated);
        Assert.Equal(ResistanceType.Band, updated!.Resistance.Type);
        Assert.Equal("Red", updated.Resistance.BandLabel);

        await using var verify = db.NewContext();
        Assert.Equal("Red", verify.WorkoutLogs.Single(l => l.Id == logId).BandLabel);
    }

    [Fact]
    public async Task UpdateLogAsync_WithExistingResistance_PreservesIt()
    {
        using var db = new TestDb();
        var poolId = await AddPoolEntryAsync(db, UserId);
        db.Context.WorkoutLogs.Add(new WorkoutLog
        {
            ExercisePoolId = poolId, CompletedQuantity = 10, Timestamp = Anchor,
            ResistanceType = ResistanceType.Weight, ResistanceAmount = 25m, WeightUnit = WeightUnit.Pounds
        });
        await db.Context.SaveChangesAsync();
        var logId = db.Context.WorkoutLogs.Single().Id;
        var existing = new ResistanceDto(ResistanceType.Weight, 25m, WeightUnit.Pounds);

        var sut = new LogService(db.Context);
        // Edit only the quantity; pass the existing resistance through (mirrors the History edit UI).
        var updated = await sut.UpdateLogAsync(UserId, logId, new UpdateLogRequest(18, Anchor, existing));

        Assert.NotNull(updated);
        Assert.Equal(18, updated!.Quantity);
        Assert.Equal(ResistanceType.Weight, updated.Resistance.Type);
        Assert.Equal(25m, updated.Resistance.Amount);
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
        db.Context.Users.Add(new ApplicationUser { Id = 2, UserName = "other@x.local", Email = "other@x.local", DisplayName = "Other" });
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
        db.Context.Users.Add(new ApplicationUser { Id = 2, UserName = "other@x.local", Email = "other@x.local", DisplayName = "Other" });
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
