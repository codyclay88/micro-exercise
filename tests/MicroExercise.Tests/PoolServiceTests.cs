using MicroExercise.Core;
using MicroExercise.Core.Dtos;
using MicroExercise.Core.Entities;
using MicroExercise.Infrastructure.Services;

namespace MicroExercise.Tests;

public class PoolServiceTests
{
    private const int UserId = AppDefaults.DemoUserId;

    [Fact]
    public async Task AddPoolItemAsync_AddsActiveEntry_ThatAppearsInActivePool()
    {
        using var db = new TestDb();
        var sut = new PoolService(db.Context);

        var created = await sut.AddPoolItemAsync(UserId,
            new CreatePoolItemRequest(ExerciseTypeId: 1, TargetQuantity: 10, CustomName: null));

        Assert.True(created.Id > 0);
        Assert.True(created.IsActive);
        Assert.Equal("Push-ups", created.DisplayName);

        var pool = await sut.GetActivePoolAsync(UserId);
        Assert.Single(pool);
        Assert.Equal(created.Id, pool[0].Id);
    }

    [Fact]
    public async Task AddPoolItemAsync_UsesCustomNameWhenProvided()
    {
        using var db = new TestDb();
        var sut = new PoolService(db.Context);

        var created = await sut.AddPoolItemAsync(UserId,
            new CreatePoolItemRequest(ExerciseTypeId: 4, TargetQuantity: 15, CustomName: "KB Swings (35 lbs)"));

        Assert.Equal("KB Swings (35 lbs)", created.DisplayName);
    }

    [Fact]
    public async Task AddPoolItemAsync_UnknownExerciseType_Throws()
    {
        using var db = new TestDb();
        var sut = new PoolService(db.Context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.AddPoolItemAsync(UserId, new CreatePoolItemRequest(ExerciseTypeId: 999, TargetQuantity: 10, CustomName: null)));
    }

    [Fact]
    public async Task AddPoolItemAsync_AppendsIncreasingSortOrder()
    {
        using var db = new TestDb();
        var sut = new PoolService(db.Context);

        var first = await sut.AddPoolItemAsync(UserId, new CreatePoolItemRequest(1, 10, null));
        var second = await sut.AddPoolItemAsync(UserId, new CreatePoolItemRequest(2, 5, null));

        Assert.Equal(0, first.SortOrder);
        Assert.Equal(1, second.SortOrder);
    }

    [Fact]
    public async Task GetActivePoolAsync_ExcludesInactive_AndOrdersBySortOrder()
    {
        using var db = new TestDb();
        db.Context.ExercisePool.AddRange(
            new ExercisePool { UserId = UserId, ExerciseTypeId = 1, TargetQuantity = 10, SortOrder = 2, IsActive = true },
            new ExercisePool { UserId = UserId, ExerciseTypeId = 2, TargetQuantity = 5, SortOrder = 0, IsActive = true },
            new ExercisePool { UserId = UserId, ExerciseTypeId = 3, TargetQuantity = 20, SortOrder = 1, IsActive = false });
        await db.Context.SaveChangesAsync();

        var sut = new PoolService(db.Context);
        var pool = await sut.GetActivePoolAsync(UserId);

        Assert.Equal(2, pool.Count);
        Assert.Equal(0, pool[0].SortOrder); // sorted ascending
        Assert.Equal(2, pool[1].SortOrder);
        Assert.DoesNotContain(pool, p => p.IsActive == false);
    }
}
