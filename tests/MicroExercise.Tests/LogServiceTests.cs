using MicroExercise.Core;
using MicroExercise.Core.Dtos;
using MicroExercise.Core.Entities;
using MicroExercise.Infrastructure.Identity;
using MicroExercise.Infrastructure.Services;

namespace MicroExercise.Tests;

public class LogServiceTests
{
    private const int UserId = TestDb.PrimaryUserId;

    private static async Task<int> AddPoolEntryAsync(TestDb db, int userId, bool isActive = true)
    {
        var entry = new ExercisePool
        {
            UserId = userId,
            ExerciseTypeId = 1,
            TargetQuantity = 10,
            IsActive = isActive
        };
        db.Context.ExercisePool.Add(entry);
        await db.Context.SaveChangesAsync();
        return entry.Id;
    }

    [Fact]
    public async Task LogAsync_OwnedActivePool_CreatesLog()
    {
        using var db = new TestDb();
        var poolId = await AddPoolEntryAsync(db, UserId);
        var sut = new LogService(db.Context);

        var result = await sut.LogAsync(UserId, new CreateLogRequest(poolId, Quantity: 12));

        Assert.NotNull(result);
        Assert.Equal(poolId, result!.ExercisePoolId);
        Assert.Equal(12, result.CompletedQuantity);
        Assert.True(result.Id > 0);

        // Persisted to the database.
        await using var verify = db.NewContext();
        Assert.Equal(1, verify.WorkoutLogs.Count());
    }

    [Fact]
    public async Task LogAsync_PoolOwnedByAnotherUser_ReturnsNull()
    {
        using var db = new TestDb();
        // Pool belongs to a different user (id 2), but seeded demo user (id 1) tries to log.
        db.Context.Users.Add(new ApplicationUser { Id = 2, UserName = "other@x.local", Email = "other@x.local", DisplayName = "Other" });
        await db.Context.SaveChangesAsync();
        var poolId = await AddPoolEntryAsync(db, userId: 2);
        var sut = new LogService(db.Context);

        var result = await sut.LogAsync(UserId, new CreateLogRequest(poolId, 10));

        Assert.Null(result);
    }

    [Fact]
    public async Task LogAsync_InactivePool_ReturnsNull()
    {
        using var db = new TestDb();
        var poolId = await AddPoolEntryAsync(db, UserId, isActive: false);
        var sut = new LogService(db.Context);

        var result = await sut.LogAsync(UserId, new CreateLogRequest(poolId, 10));

        Assert.Null(result);
    }

    [Fact]
    public async Task LogAsync_UnknownPool_ReturnsNull()
    {
        using var db = new TestDb();
        var sut = new LogService(db.Context);

        var result = await sut.LogAsync(UserId, new CreateLogRequest(ExercisePoolId: 12345, Quantity: 10));

        Assert.Null(result);
    }
}
