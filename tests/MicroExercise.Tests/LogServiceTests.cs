using MicroExercise.Core;
using MicroExercise.Core.Dtos;
using MicroExercise.Core.Entities;
using MicroExercise.Core.Enums;
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
            LastQuantity = 10,
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
    public async Task LogAsync_OverwritesPoolLastQuantity()
    {
        using var db = new TestDb();
        var poolId = await AddPoolEntryAsync(db, UserId);   // seeded LastQuantity = 10
        var sut = new LogService(db.Context);

        await sut.LogAsync(UserId, new CreateLogRequest(poolId, Quantity: 27));

        await using var verify = db.NewContext();
        Assert.Equal(27, verify.ExercisePool.Single(p => p.Id == poolId).LastQuantity);
    }

    [Fact]
    public async Task LogAsync_NoResistance_DefaultsToBodyweight()
    {
        using var db = new TestDb();
        var poolId = await AddPoolEntryAsync(db, UserId);
        var sut = new LogService(db.Context);

        var result = await sut.LogAsync(UserId, new CreateLogRequest(poolId, 10));

        Assert.NotNull(result);
        Assert.Equal(ResistanceType.Bodyweight, result!.Resistance.Type);
        Assert.True(result.Resistance.IsBodyweight);

        await using var verify = db.NewContext();
        var row = verify.WorkoutLogs.Single();
        Assert.Equal(ResistanceType.Bodyweight, row.ResistanceType);
        Assert.Null(row.ResistanceAmount);
        Assert.Null(row.WeightUnit);
        Assert.Null(row.BandLabel);
    }

    [Fact]
    public async Task LogAsync_WeightResistance_PersistsAmountAndUnit()
    {
        using var db = new TestDb();
        var poolId = await AddPoolEntryAsync(db, UserId);
        var sut = new LogService(db.Context);

        var resistance = new ResistanceDto(ResistanceType.Weight, 15m, WeightUnit.Kilograms);
        var result = await sut.LogAsync(UserId, new CreateLogRequest(poolId, 10, resistance));

        Assert.NotNull(result);
        Assert.Equal(ResistanceType.Weight, result!.Resistance.Type);
        Assert.Equal(15m, result.Resistance.Amount);
        Assert.Equal(WeightUnit.Kilograms, result.Resistance.Unit);
        Assert.Equal("15 kg", result.Resistance.Describe());

        await using var verify = db.NewContext();
        var row = verify.WorkoutLogs.Single();
        Assert.Equal(ResistanceType.Weight, row.ResistanceType);
        Assert.Equal(15m, row.ResistanceAmount);
        Assert.Equal(WeightUnit.Kilograms, row.WeightUnit);
        Assert.Null(row.BandLabel);
    }

    [Fact]
    public async Task LogAsync_BandResistance_PersistsTrimmedLabel()
    {
        using var db = new TestDb();
        var poolId = await AddPoolEntryAsync(db, UserId);
        var sut = new LogService(db.Context);

        var resistance = new ResistanceDto(ResistanceType.Band, BandLabel: "  Green  ");
        var result = await sut.LogAsync(UserId, new CreateLogRequest(poolId, 10, resistance));

        Assert.NotNull(result);
        Assert.Equal(ResistanceType.Band, result!.Resistance.Type);
        Assert.Equal("Green", result.Resistance.BandLabel);
        Assert.Equal("Green band", result.Resistance.Describe());

        await using var verify = db.NewContext();
        var row = verify.WorkoutLogs.Single();
        Assert.Equal("Green", row.BandLabel);
        Assert.Null(row.ResistanceAmount);
        Assert.Null(row.WeightUnit);
    }

    [Theory]
    [InlineData(ResistanceType.Weight, null, null)]   // weight with no amount -> bodyweight
    [InlineData(ResistanceType.Band, null, "   ")]     // band with blank label -> bodyweight
    public async Task LogAsync_IncompleteResistance_NormalizesToBodyweight(
        ResistanceType type, double? amount, string? label)
    {
        using var db = new TestDb();
        var poolId = await AddPoolEntryAsync(db, UserId);
        var sut = new LogService(db.Context);

        var resistance = new ResistanceDto(type, (decimal?)amount, WeightUnit.Pounds, label);
        var result = await sut.LogAsync(UserId, new CreateLogRequest(poolId, 10, resistance));

        Assert.NotNull(result);
        Assert.Equal(ResistanceType.Bodyweight, result!.Resistance.Type);

        await using var verify = db.NewContext();
        var row = verify.WorkoutLogs.Single();
        Assert.Equal(ResistanceType.Bodyweight, row.ResistanceType);
        Assert.Null(row.ResistanceAmount);
        Assert.Null(row.WeightUnit);
        Assert.Null(row.BandLabel);
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
