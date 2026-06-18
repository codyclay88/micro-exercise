using MicroExercise.Core.Dtos;
using MicroExercise.Core.Entities;
using MicroExercise.Core.Enums;
using MicroExercise.Infrastructure.Identity;
using MicroExercise.Infrastructure.Services;

namespace MicroExercise.Tests;

public class CustomExerciseTests
{
    private const int UserId = TestDb.PrimaryUserId;

    [Fact]
    public async Task AddCustomExerciseAsync_CreatesOwnedReptExercise_AndAddsToPool()
    {
        using var db = new TestDb();
        var sut = new PoolService(db.Context);

        var created = await sut.AddCustomExerciseAsync(UserId,
            new CreateCustomExerciseRequest("Bunny Hops", TrackingType.Reps, 100));

        Assert.Equal("Bunny Hops", created.DisplayName);
        Assert.Equal(TrackingType.Reps, created.TrackingType);
        Assert.Equal(100, created.LastQuantity);
        Assert.True(created.IsActive);

        // It appears in the user's catalog as a custom entry...
        var types = await sut.GetExerciseTypesAsync(UserId);
        Assert.Contains(types, t => t.Name == "Bunny Hops" && t.IsCustom && t.DefaultTrackingType == TrackingType.Reps);

        // ...and in their active pool.
        var pool = await sut.GetActivePoolAsync(UserId);
        Assert.Contains(pool, p => p.DisplayName == "Bunny Hops" && p.LastQuantity == 100);
    }

    [Fact]
    public async Task AddCustomExerciseAsync_SupportsTimeBasedExercise()
    {
        using var db = new TestDb();
        var sut = new PoolService(db.Context);

        var created = await sut.AddCustomExerciseAsync(UserId,
            new CreateCustomExerciseRequest("Planks", TrackingType.Seconds, 30));

        Assert.Equal(TrackingType.Seconds, created.TrackingType);
        Assert.Equal(30, created.LastQuantity);
    }

    [Fact]
    public async Task AddCustomExerciseAsync_BlankName_Throws()
    {
        using var db = new TestDb();
        var sut = new PoolService(db.Context);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.AddCustomExerciseAsync(UserId, new CreateCustomExerciseRequest("   ", TrackingType.Reps, 10)));
    }

    [Fact]
    public async Task GetExerciseTypesAsync_IncludesGlobalAndOwnCustom_ExcludesOtherUsers()
    {
        using var db = new TestDb();
        db.Context.Users.Add(new ApplicationUser { Id = 2, UserName = "other@x.local", Email = "other@x.local", DisplayName = "Other" });
        db.Context.ExerciseTypes.AddRange(
            new ExerciseType { Name = "Mine", DefaultTrackingType = TrackingType.Reps, OwnerUserId = UserId },
            new ExerciseType { Name = "Theirs", DefaultTrackingType = TrackingType.Reps, OwnerUserId = 2 });
        await db.Context.SaveChangesAsync();

        var sut = new PoolService(db.Context);
        var types = await sut.GetExerciseTypesAsync(UserId);

        Assert.Contains(types, t => t.Name == "Mine" && t.IsCustom);
        Assert.DoesNotContain(types, t => t.Name == "Theirs");
        Assert.Contains(types, t => t.Name == "Push-ups" && !t.IsCustom); // global catalog still present
    }
}
