using MicroExercise.Core.Dtos;
using MicroExercise.Core.Entities;
using MicroExercise.Core.Enums;
using MicroExercise.Infrastructure.Identity;
using MicroExercise.Infrastructure.Services;

namespace MicroExercise.Tests;

public class GoalServiceTests
{
    private const int UserId = TestDb.PrimaryUserId;

    private static ExercisePool AddPool(TestDb db, int userId = UserId, int typeId = 1, bool active = true)
    {
        var pool = new ExercisePool { UserId = userId, ExerciseTypeId = typeId, TargetQuantity = 10, IsActive = active };
        db.Context.ExercisePool.Add(pool);
        db.Context.SaveChanges();
        return pool;
    }

    [Fact]
    public async Task CreateGoalAsync_CountsOnlyInWindowBursts_AndReportsActive()
    {
        using var db = new TestDb();
        var pool = AddPool(db);
        var now = DateTimeOffset.Now;
        db.Context.WorkoutLogs.AddRange(
            new WorkoutLog { ExercisePoolId = pool.Id, CompletedQuantity = 20, Timestamp = now.AddHours(-2) },
            new WorkoutLog { ExercisePoolId = pool.Id, CompletedQuantity = 30, Timestamp = now.AddHours(-1) },
            // Before the (backdated) start — must be excluded.
            new WorkoutLog { ExercisePoolId = pool.Id, CompletedQuantity = 99, Timestamp = now.AddDays(-10) });
        await db.Context.SaveChangesAsync();

        var sut = new GoalService(db.Context);
        var goal = await sut.CreateGoalAsync(UserId,
            new CreateGoalRequest(pool.Id, 100, now.AddDays(3), now.AddDays(-1)));

        Assert.NotNull(goal);
        Assert.Equal(50, goal!.CurrentProgress); // 20 + 30; the -10d burst is out of window
        Assert.Equal(50, goal.RemainingQuantity);
        Assert.Equal(50.0, goal.PercentComplete);
        Assert.Equal(GoalStatus.Active, goal.Status);
    }

    [Fact]
    public async Task CreateGoalAsync_AchievedWhenTargetMet()
    {
        using var db = new TestDb();
        var pool = AddPool(db);
        var now = DateTimeOffset.Now;
        db.Context.WorkoutLogs.Add(
            new WorkoutLog { ExercisePoolId = pool.Id, CompletedQuantity = 120, Timestamp = now.AddHours(-1) });
        await db.Context.SaveChangesAsync();

        var sut = new GoalService(db.Context);
        var goal = await sut.CreateGoalAsync(UserId,
            new CreateGoalRequest(pool.Id, 100, now.AddDays(3), now.AddDays(-1)));

        Assert.NotNull(goal);
        Assert.Equal(GoalStatus.Achieved, goal!.Status);
        Assert.Equal(120, goal.CurrentProgress);
        Assert.Equal(0, goal.RemainingQuantity);
        Assert.Equal(100.0, goal.PercentComplete); // clamped, never over 100
    }

    [Fact]
    public async Task GetGoalsAsync_ExpiredWhenDeadlinePassedUnmet_AndExcludedFromActiveFilter()
    {
        using var db = new TestDb();
        var pool = AddPool(db);
        var now = DateTimeOffset.Now;
        db.Context.Goals.Add(new Goal
        {
            UserId = UserId,
            ExercisePoolId = pool.Id,
            TargetQuantity = 100,
            StartDate = now.AddDays(-5),
            Deadline = now.AddDays(-1),
            CreatedAt = now.AddDays(-5)
        });
        await db.Context.SaveChangesAsync();

        var sut = new GoalService(db.Context);

        var all = await sut.GetGoalsAsync(UserId, includeCompleted: true);
        var g = Assert.Single(all);
        Assert.Equal(GoalStatus.Expired, g.Status);
        Assert.Equal(0, g.CurrentProgress);

        var activeOnly = await sut.GetGoalsAsync(UserId, includeCompleted: false);
        Assert.Empty(activeOnly);
    }

    [Fact]
    public async Task CreateGoalAsync_ReturnsNull_ForMissingOrInactivePoolItem()
    {
        using var db = new TestDb();
        var now = DateTimeOffset.Now;
        var sut = new GoalService(db.Context);

        // Non-existent pool item.
        Assert.Null(await sut.CreateGoalAsync(UserId,
            new CreateGoalRequest(9999, 100, now.AddDays(3), now)));

        // Inactive (soft-deleted) pool item.
        var inactive = AddPool(db, active: false);
        Assert.Null(await sut.CreateGoalAsync(UserId,
            new CreateGoalRequest(inactive.Id, 100, now.AddDays(3), now)));
    }

    [Fact]
    public async Task Goals_AreIsolatedPerUser()
    {
        using var db = new TestDb();
        db.Context.Users.Add(new ApplicationUser
        {
            Id = 2,
            UserName = "other@test.local",
            Email = "other@test.local",
            DisplayName = "Other"
        });
        db.Context.SaveChanges();
        var otherPool = AddPool(db, userId: 2);
        var now = DateTimeOffset.Now;

        var sut = new GoalService(db.Context);
        var created = await sut.CreateGoalAsync(2,
            new CreateGoalRequest(otherPool.Id, 50, now.AddDays(3), now.AddDays(-1)));
        Assert.NotNull(created);

        // The primary user can neither see nor delete user 2's goal.
        Assert.Empty(await sut.GetGoalsAsync(UserId));
        Assert.False(await sut.DeleteGoalAsync(UserId, created!.Id));

        // The owner can delete it.
        Assert.True(await sut.DeleteGoalAsync(2, created.Id));
    }
}
