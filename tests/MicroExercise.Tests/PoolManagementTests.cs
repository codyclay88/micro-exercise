using MicroExercise.Core;
using MicroExercise.Core.Dtos;
using MicroExercise.Core.Entities;
using MicroExercise.Infrastructure.Identity;
using MicroExercise.Infrastructure.Services;

namespace MicroExercise.Tests;

public class PoolManagementTests
{
    private const int UserId = TestDb.PrimaryUserId;

    private static async Task<ExercisePool> AddEntryAsync(
        TestDb db, int userId, int exerciseTypeId, int sortOrder, string? custom = null)
    {
        var entry = new ExercisePool
        {
            UserId = userId,
            ExerciseTypeId = exerciseTypeId,
            LastQuantity = 10,
            SortOrder = sortOrder,
            CustomName = custom
        };
        db.Context.ExercisePool.Add(entry);
        await db.Context.SaveChangesAsync();
        return entry;
    }

    [Fact]
    public async Task UpdatePoolItemAsync_OwnedEntry_ChangesNameAndTarget()
    {
        using var db = new TestDb();
        var entry = await AddEntryAsync(db, UserId, exerciseTypeId: 4, sortOrder: 0);
        var sut = new PoolService(db.Context);

        var updated = await sut.UpdatePoolItemAsync(UserId, entry.Id,
            new UpdatePoolItemRequest("KB RDL (35 lbs)", 15));

        Assert.NotNull(updated);
        Assert.Equal("KB RDL (35 lbs)", updated!.DisplayName);
        Assert.Equal(15, updated.LastQuantity);
    }

    [Fact]
    public async Task UpdatePoolItemAsync_BlankCustomName_FallsBackToTypeName()
    {
        using var db = new TestDb();
        var entry = await AddEntryAsync(db, UserId, exerciseTypeId: 1, sortOrder: 0, custom: "My Push-ups");
        var sut = new PoolService(db.Context);

        var updated = await sut.UpdatePoolItemAsync(UserId, entry.Id, new UpdatePoolItemRequest("   ", 10));

        Assert.NotNull(updated);
        Assert.Equal("Push-ups", updated!.DisplayName); // cleared -> type name
    }

    [Fact]
    public async Task UpdatePoolItemAsync_NotOwned_ReturnsNull()
    {
        using var db = new TestDb();
        db.Context.Users.Add(new ApplicationUser { Id = 2, UserName = "other@x.local", Email = "other@x.local", DisplayName = "Other" });
        await db.Context.SaveChangesAsync();
        var entry = await AddEntryAsync(db, userId: 2, exerciseTypeId: 1, sortOrder: 0);
        var sut = new PoolService(db.Context);

        var updated = await sut.UpdatePoolItemAsync(UserId, entry.Id, new UpdatePoolItemRequest("Hijack", 99));

        Assert.Null(updated);
    }

    [Fact]
    public async Task MovePoolItemAsync_Down_SwapsOrderWithNeighbour()
    {
        using var db = new TestDb();
        var a = await AddEntryAsync(db, UserId, exerciseTypeId: 1, sortOrder: 0); // Push-ups
        var b = await AddEntryAsync(db, UserId, exerciseTypeId: 2, sortOrder: 1); // Pull-ups
        var sut = new PoolService(db.Context);

        var moved = await sut.MovePoolItemAsync(UserId, a.Id, up: false);

        Assert.True(moved);
        var pool = await sut.GetActivePoolAsync(UserId);
        Assert.Equal(b.Id, pool[0].Id); // Pull-ups now first
        Assert.Equal(a.Id, pool[1].Id);
    }

    [Fact]
    public async Task MovePoolItemAsync_UpAtTop_ReturnsFalse()
    {
        using var db = new TestDb();
        var a = await AddEntryAsync(db, UserId, exerciseTypeId: 1, sortOrder: 0);
        await AddEntryAsync(db, UserId, exerciseTypeId: 2, sortOrder: 1);
        var sut = new PoolService(db.Context);

        var moved = await sut.MovePoolItemAsync(UserId, a.Id, up: true);

        Assert.False(moved);
    }

    [Fact]
    public async Task DeactivatePoolItemAsync_RemovesFromActive_ButKeepsHistory()
    {
        using var db = new TestDb();
        var entry = await AddEntryAsync(db, UserId, exerciseTypeId: 1, sortOrder: 0);
        db.Context.WorkoutLogs.Add(new WorkoutLog
        {
            ExercisePoolId = entry.Id,
            CompletedQuantity = 10,
            Timestamp = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero)
        });
        await db.Context.SaveChangesAsync();
        var sut = new PoolService(db.Context);

        var removed = await sut.DeactivatePoolItemAsync(UserId, entry.Id);

        Assert.True(removed);
        var pool = await sut.GetActivePoolAsync(UserId);
        Assert.Empty(pool);

        await using var verify = db.NewContext();
        Assert.Equal(1, verify.WorkoutLogs.Count(l => l.ExercisePoolId == entry.Id)); // history preserved
        Assert.False(verify.ExercisePool.Single(p => p.Id == entry.Id).IsActive);
    }

    [Fact]
    public async Task DeactivatePoolItemAsync_NotOwned_ReturnsFalse()
    {
        using var db = new TestDb();
        db.Context.Users.Add(new ApplicationUser { Id = 2, UserName = "other@x.local", Email = "other@x.local", DisplayName = "Other" });
        await db.Context.SaveChangesAsync();
        var entry = await AddEntryAsync(db, userId: 2, exerciseTypeId: 1, sortOrder: 0);
        var sut = new PoolService(db.Context);

        var removed = await sut.DeactivatePoolItemAsync(UserId, entry.Id);

        Assert.False(removed);
    }
}
