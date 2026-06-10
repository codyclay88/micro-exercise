using MicroExercise.Core.Abstractions;
using MicroExercise.Core.Dtos;
using MicroExercise.Core.Entities;
using MicroExercise.Core.Enums;
using MicroExercise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MicroExercise.Infrastructure.Services;

public class GoalService(AppDbContext db) : IGoalService
{
    public async Task<GoalDto?> CreateGoalAsync(int userId, CreateGoalRequest request, CancellationToken ct = default)
    {
        // A goal can only target an active pool item the user owns.
        var owns = await db.ExercisePool
            .AnyAsync(p => p.Id == request.ExercisePoolId && p.UserId == userId && p.IsActive, ct);
        if (!owns)
            return null;

        var goal = new Goal
        {
            UserId = userId,
            ExercisePoolId = request.ExercisePoolId,
            TargetQuantity = request.TargetQuantity,
            StartDate = request.StartDate ?? DateTimeOffset.Now,
            Deadline = request.Deadline,
            CreatedAt = DateTimeOffset.Now
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync(ct);

        return await GetGoalAsync(userId, goal.Id, ct);
    }

    public async Task<IReadOnlyList<GoalDto>> GetGoalsAsync(
        int userId, bool includeCompleted = true, CancellationToken ct = default)
    {
        var now = DateTimeOffset.Now;
        var rows = await Project(db.Goals
                .Where(g => g.UserId == userId)
                .OrderByDescending(g => g.CreatedAt))
            .ToListAsync(ct);

        IEnumerable<GoalDto> dtos = rows.Select(r => ToDto(r, now));
        if (!includeCompleted)
            dtos = dtos.Where(d => d.Status == GoalStatus.Active);
        return dtos.ToList();
    }

    public async Task<GoalDto?> GetGoalAsync(int userId, int goalId, CancellationToken ct = default)
    {
        var row = await Project(db.Goals.Where(g => g.Id == goalId && g.UserId == userId))
            .FirstOrDefaultAsync(ct);
        return row is null ? null : ToDto(row, DateTimeOffset.Now);
    }

    public async Task<bool> DeleteGoalAsync(int userId, int goalId, CancellationToken ct = default)
    {
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId, ct);
        if (goal is null)
            return false;

        db.Goals.Remove(goal);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Projects goals to flat rows, computing progress via a correlated SUM over the bursts in
    /// each goal's window. Scalar subquery (no GroupBy), so it translates cleanly on both providers.
    /// </summary>
    private IQueryable<GoalRow> Project(IQueryable<Goal> goals) =>
        goals.Select(g => new GoalRow(
            g.Id,
            g.ExercisePoolId,
            g.ExercisePool!.CustomName ?? g.ExercisePool.ExerciseType!.Name,
            g.ExercisePool!.ExerciseType!.DefaultTrackingType,
            g.TargetQuantity,
            db.WorkoutLogs
                .Where(l => l.ExercisePoolId == g.ExercisePoolId
                            && l.Timestamp >= g.StartDate
                            && l.Timestamp <= g.Deadline)
                .Sum(l => (int?)l.CompletedQuantity) ?? 0,
            g.StartDate,
            g.Deadline));

    private static GoalDto ToDto(GoalRow r, DateTimeOffset now)
    {
        var remaining = Math.Max(0, r.TargetQuantity - r.Progress);
        var percent = r.TargetQuantity <= 0
            ? 0
            : Math.Min(100, Math.Round(100.0 * r.Progress / r.TargetQuantity, 1));
        var status = r.Progress >= r.TargetQuantity
            ? GoalStatus.Achieved
            : now > r.Deadline
                ? GoalStatus.Expired
                : GoalStatus.Active;

        return new GoalDto(
            r.Id, r.ExercisePoolId, r.Name, r.TrackingType, r.TargetQuantity,
            r.Progress, remaining, percent, r.StartDate, r.Deadline, status);
    }

    private sealed record GoalRow(
        int Id,
        int ExercisePoolId,
        string Name,
        TrackingType TrackingType,
        int TargetQuantity,
        int Progress,
        DateTimeOffset StartDate,
        DateTimeOffset Deadline);
}
