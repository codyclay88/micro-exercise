using MicroExercise.Core.Enums;

namespace MicroExercise.Core.Dtos;

/// <summary>A global exercise available for a user to add to their pool (spec §4.2 discovery).</summary>
public record ExerciseTypeDto(int Id, string Name, TrackingType DefaultTrackingType);
