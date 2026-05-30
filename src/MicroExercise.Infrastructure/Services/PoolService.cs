using MicroExercise.Core.Abstractions;
using MicroExercise.Core.Dtos;
using MicroExercise.Core.Entities;
using MicroExercise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MicroExercise.Infrastructure.Services;

public class PoolService(AppDbContext db) : IPoolService
{
    public async Task<IReadOnlyList<PoolItemDto>> GetActivePoolAsync(int userId, CancellationToken ct = default)
    {
        return await db.ExercisePool
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderBy(p => p.SortOrder)
            .Select(p => new PoolItemDto(
                p.Id,
                p.ExerciseTypeId,
                p.CustomName ?? p.ExerciseType!.Name,
                p.ExerciseType!.DefaultTrackingType,
                p.TargetQuantity,
                p.SortOrder,
                p.IsActive))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ExerciseTypeDto>> GetExerciseTypesAsync(CancellationToken ct = default)
    {
        return await db.ExerciseTypes
            .OrderBy(t => t.Name)
            .Select(t => new ExerciseTypeDto(t.Id, t.Name, t.DefaultTrackingType))
            .ToListAsync(ct);
    }

    public async Task<PoolItemDto> AddPoolItemAsync(int userId, CreatePoolItemRequest request, CancellationToken ct = default)
    {
        var exerciseType = await db.ExerciseTypes.FirstOrDefaultAsync(t => t.Id == request.ExerciseTypeId, ct)
            ?? throw new KeyNotFoundException($"ExerciseType {request.ExerciseTypeId} was not found.");

        // New cards append to the end of the user's current grid order.
        var nextSortOrder = await db.ExercisePool
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.SortOrder)
            .MaxAsync(ct) + 1 ?? 0;

        var customName = string.IsNullOrWhiteSpace(request.CustomName) ? null : request.CustomName.Trim();

        var entry = new ExercisePool
        {
            UserId = userId,
            ExerciseTypeId = request.ExerciseTypeId,
            CustomName = customName,
            TargetQuantity = request.TargetQuantity,
            SortOrder = nextSortOrder,
            IsActive = true
        };

        db.ExercisePool.Add(entry);
        await db.SaveChangesAsync(ct);

        return new PoolItemDto(
            entry.Id,
            entry.ExerciseTypeId,
            entry.CustomName ?? exerciseType.Name,
            exerciseType.DefaultTrackingType,
            entry.TargetQuantity,
            entry.SortOrder,
            entry.IsActive);
    }

    public async Task<PoolItemDto?> UpdatePoolItemAsync(
        int userId, int poolId, UpdatePoolItemRequest request, CancellationToken ct = default)
    {
        var entry = await db.ExercisePool
            .Include(p => p.ExerciseType)
            .FirstOrDefaultAsync(p => p.Id == poolId && p.UserId == userId, ct);
        if (entry is null)
            return null;

        entry.CustomName = string.IsNullOrWhiteSpace(request.CustomName) ? null : request.CustomName.Trim();
        entry.TargetQuantity = request.TargetQuantity;
        await db.SaveChangesAsync(ct);

        return new PoolItemDto(
            entry.Id,
            entry.ExerciseTypeId,
            entry.CustomName ?? entry.ExerciseType!.Name,
            entry.ExerciseType!.DefaultTrackingType,
            entry.TargetQuantity,
            entry.SortOrder,
            entry.IsActive);
    }

    public async Task<bool> MovePoolItemAsync(int userId, int poolId, bool up, CancellationToken ct = default)
    {
        var active = await db.ExercisePool
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);

        var index = active.FindIndex(p => p.Id == poolId);
        if (index < 0)
            return false;

        var neighbourIndex = up ? index - 1 : index + 1;
        if (neighbourIndex < 0 || neighbourIndex >= active.Count)
            return false;

        // Swap sort positions with the neighbour.
        (active[index].SortOrder, active[neighbourIndex].SortOrder) =
            (active[neighbourIndex].SortOrder, active[index].SortOrder);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeactivatePoolItemAsync(int userId, int poolId, CancellationToken ct = default)
    {
        var entry = await db.ExercisePool
            .FirstOrDefaultAsync(p => p.Id == poolId && p.UserId == userId && p.IsActive, ct);
        if (entry is null)
            return false;

        entry.IsActive = false; // soft delete — historical logs are preserved
        await db.SaveChangesAsync(ct);
        return true;
    }
}
