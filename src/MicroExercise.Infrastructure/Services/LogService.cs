using MicroExercise.Core.Abstractions;
using MicroExercise.Core.Dtos;
using MicroExercise.Core.Entities;
using MicroExercise.Core.Enums;
using MicroExercise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MicroExercise.Infrastructure.Services;

public class LogService(AppDbContext db) : ILogService
{
    public async Task<LogResultDto?> LogAsync(int userId, CreateLogRequest request, CancellationToken ct = default)
    {
        // Only log against an active pool entry the caller actually owns.
        var pool = await db.ExercisePool
            .FirstOrDefaultAsync(p => p.Id == request.ExercisePoolId && p.UserId == userId && p.IsActive, ct);
        if (pool is null)
            return null;

        var log = new WorkoutLog
        {
            ExercisePoolId = request.ExercisePoolId,
            CompletedQuantity = request.Quantity,
            Timestamp = DateTimeOffset.Now
        };
        ApplyResistance(log, request.Resistance);

        // Remember this burst's amount so it pre-fills the next one (the "last amount" model).
        pool.LastQuantity = request.Quantity;

        db.WorkoutLogs.Add(log);
        await db.SaveChangesAsync(ct);

        return new LogResultDto(log.Id, log.ExercisePoolId, log.CompletedQuantity, log.Timestamp, ToResistance(log));
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
                l.Timestamp,
                new ResistanceDto(l.ResistanceType, l.ResistanceAmount, l.WeightUnit, l.BandLabel)))
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
        ApplyResistance(log, request.Resistance);
        await db.SaveChangesAsync(ct);

        return new BurstDto(
            log.Id,
            log.ExercisePoolId,
            log.ExercisePool!.CustomName ?? log.ExercisePool.ExerciseType!.Name,
            log.ExercisePool.ExerciseType!.DefaultTrackingType,
            log.CompletedQuantity,
            log.Timestamp,
            ToResistance(log));
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

    /// <summary>
    /// Writes a (possibly malformed or null) resistance request onto the entity, coercing each
    /// shape to a consistent state: Weight keeps amount + unit (defaulting to lbs), Band keeps a
    /// trimmed non-empty label, and anything else collapses to plain Bodyweight with null fields.
    /// </summary>
    private static void ApplyResistance(WorkoutLog log, ResistanceDto? resistance)
    {
        switch (resistance)
        {
            case { Type: ResistanceType.Weight, Amount: > 0 } w:
                log.ResistanceType = ResistanceType.Weight;
                log.ResistanceAmount = w.Amount;
                log.WeightUnit = w.Unit ?? Core.Enums.WeightUnit.Pounds;
                log.BandLabel = null;
                break;

            case { Type: ResistanceType.Band } b when !string.IsNullOrWhiteSpace(b.BandLabel):
                log.ResistanceType = ResistanceType.Band;
                log.BandLabel = b.BandLabel.Trim();
                log.ResistanceAmount = null;
                log.WeightUnit = null;
                break;

            default:
                log.ResistanceType = ResistanceType.Bodyweight;
                log.ResistanceAmount = null;
                log.WeightUnit = null;
                log.BandLabel = null;
                break;
        }
    }

    private static ResistanceDto ToResistance(WorkoutLog log) =>
        new(log.ResistanceType, log.ResistanceAmount, log.WeightUnit, log.BandLabel);
}
