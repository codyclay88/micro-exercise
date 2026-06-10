namespace MicroExercise.Core.Dtos;

/// <summary>
/// Create a one-shot, deadline-bound goal against an owned pool item.
/// <see cref="StartDate"/> defaults to "now" when null (bursts logged on/after it count).
/// </summary>
public record CreateGoalRequest(
    int ExercisePoolId,
    int TargetQuantity,
    DateTimeOffset Deadline,
    DateTimeOffset? StartDate);
