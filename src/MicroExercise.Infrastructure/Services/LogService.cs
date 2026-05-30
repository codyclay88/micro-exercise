using MicroExercise.Core.Abstractions;
using MicroExercise.Core.Dtos;
using MicroExercise.Core.Entities;
using MicroExercise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MicroExercise.Infrastructure.Services;

public class LogService(AppDbContext db) : ILogService
{
    public async Task<LogResultDto?> LogAsync(int userId, CreateLogRequest request, CancellationToken ct = default)
    {
        // Only log against an active pool entry the caller actually owns.
        var owns = await db.ExercisePool
            .AnyAsync(p => p.Id == request.ExercisePoolId && p.UserId == userId && p.IsActive, ct);
        if (!owns)
            return null;

        var log = new WorkoutLog
        {
            ExercisePoolId = request.ExercisePoolId,
            CompletedQuantity = request.Quantity,
            Timestamp = DateTimeOffset.Now
        };

        db.WorkoutLogs.Add(log);
        await db.SaveChangesAsync(ct);

        return new LogResultDto(log.Id, log.ExercisePoolId, log.CompletedQuantity, log.Timestamp);
    }

    public async Task<IReadOnlyList<BurstDto>> GetHistoryAsync(
        int userId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
    {
        return await db.WorkoutLogs
            .Where(l => l.ExercisePool!.UserId == userId
                        && l.Timestamp >= start
                        && l.Timestamp <= end)
            .OrderByDescending(l => l.Timestamp)
            .Select(l => new BurstDto(
                l.Id,
                l.ExercisePoolId,
                l.ExercisePool!.CustomName ?? l.ExercisePool.ExerciseType!.Name,
                l.ExercisePool!.ExerciseType!.DefaultTrackingType,
                l.CompletedQuantity,
                l.Timestamp))
            .ToListAsync(ct);
    }

    public async Task<BurstDto?> UpdateLogAsync(
        int userId, long logId, UpdateLogRequest request, CancellationToken ct = default)
    {
        // Load with the pool/type so we can both authorize and project the result.
        var log = await db.WorkoutLogs
            .Include(l => l.ExercisePool!).ThenInclude(p => p.ExerciseType)
            .FirstOrDefaultAsync(l => l.Id == logId && l.ExercisePool!.UserId == userId, ct);
        if (log is null)
            return null;

        log.CompletedQuantity = request.Quantity;
        log.Timestamp = request.Timestamp;
        await db.SaveChangesAsync(ct);

        return new BurstDto(
            log.Id,
            log.ExercisePoolId,
            log.ExercisePool!.CustomName ?? log.ExercisePool.ExerciseType!.Name,
            log.ExercisePool.ExerciseType!.DefaultTrackingType,
            log.CompletedQuantity,
            log.Timestamp);
    }

    public async Task<bool> DeleteLogAsync(int userId, long logId, CancellationToken ct = default)
    {
        var log = await db.WorkoutLogs
            .FirstOrDefaultAsync(l => l.Id == logId && l.ExercisePool!.UserId == userId, ct);
        if (log is null)
            return false;

        db.WorkoutLogs.Remove(log);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
