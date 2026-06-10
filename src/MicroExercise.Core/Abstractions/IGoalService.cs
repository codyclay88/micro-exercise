using MicroExercise.Core.Dtos;

namespace MicroExercise.Core.Abstractions;

/// <summary>
/// One-shot, deadline-bound goals. Progress is derived from logged bursts in each goal's
/// window; status (Active/Achieved/Expired) is computed on read.
/// </summary>
public interface IGoalService
{
    /// <summary>
    /// Creates a goal against an active pool item the user owns. Returns the created goal with
    /// its current progress, or <c>null</c> if the pool item isn't found, owned, or active.
    /// </summary>
    Task<GoalDto?> CreateGoalAsync(int userId, CreateGoalRequest request, CancellationToken ct = default);

    /// <summary>The user's goals with live progress, newest first. Set
    /// <paramref name="includeCompleted"/> false to return only <c>Active</c> goals.</summary>
    Task<IReadOnlyList<GoalDto>> GetGoalsAsync(int userId, bool includeCompleted = true, CancellationToken ct = default);

    Task<GoalDto?> GetGoalAsync(int userId, int goalId, CancellationToken ct = default);

    /// <summary>Hard-deletes a goal. Returns false if it isn't found or owned. History is unaffected.</summary>
    Task<bool> DeleteGoalAsync(int userId, int goalId, CancellationToken ct = default);
}
