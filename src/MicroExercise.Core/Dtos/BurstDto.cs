using MicroExercise.Core.Enums;

namespace MicroExercise.Core.Dtos;

/// <summary>
/// A single recorded burst with enough context to display and edit it in the
/// history view (spec §4.4). <see cref="ExerciseName"/> resolves the custom
/// override or the underlying exercise type name.
/// </summary>
public record BurstDto(
    long Id,
    int ExercisePoolId,
    string ExerciseName,
    TrackingType TrackingType,
    int Quantity,
    DateTimeOffset Timestamp,
    ResistanceDto Resistance);
