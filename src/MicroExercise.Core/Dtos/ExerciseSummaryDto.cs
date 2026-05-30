using MicroExercise.Core.Enums;

namespace MicroExercise.Core.Dtos;

/// <summary>
/// Aggregated volume for one pool item over a date range (spec §4.3 / §5.1):
/// the SUM of completed quantity and the COUNT of bursts.
/// </summary>
public record ExerciseSummaryDto(
    int ExercisePoolId,
    string ExerciseName,
    TrackingType TrackingType,
    long TotalVolume,
    int TotalBursts);
