using MicroExercise.Core.Enums;

namespace MicroExercise.Core.Dtos;

/// <summary>
/// A goal with its live, computed progress. Progress, remaining, percent, and status are
/// all derived from the bursts logged in the goal's window — not stored.
/// </summary>
public record GoalDto(
    int Id,
    int ExercisePoolId,
    string ExerciseName,
    TrackingType TrackingType,
    int TargetQuantity,
    int CurrentProgress,
    int RemainingQuantity,
    double PercentComplete,
    DateTimeOffset StartDate,
    DateTimeOffset Deadline,
    GoalStatus Status);
