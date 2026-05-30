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
}
