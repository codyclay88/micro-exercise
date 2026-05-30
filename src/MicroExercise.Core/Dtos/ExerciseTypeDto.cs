using MicroExercise.Core.Enums;

namespace MicroExercise.Core.Dtos;

/// <summary>
/// An exercise available for a user to add to their pool (spec §4.2 discovery): either a
/// global catalog entry or one of the user's own custom exercises (<see cref="IsCustom"/>).
/// </summary>
public record ExerciseTypeDto(int Id, string Name, TrackingType DefaultTrackingType, bool IsCustom);
