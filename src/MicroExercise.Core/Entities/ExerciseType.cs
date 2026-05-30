using MicroExercise.Core.Enums;

namespace MicroExercise.Core.Entities;

/// <summary>
/// A globally-defined, system lookup exercise (e.g. "Push-ups", "Dead Hang").
/// Users draw from these when building their personal <see cref="ExercisePool"/>.
/// </summary>
public class ExerciseType
{
    public int Id { get; set; }

    /// <summary>Display name of the movement, e.g. "Push-ups", "KB Swings".</summary>
    public required string Name { get; set; }

    /// <summary>Whether this movement is measured in reps or seconds by default.</summary>
    public TrackingType DefaultTrackingType { get; set; }

    /// <summary>User pool entries derived from this exercise type.</summary>
    public ICollection<ExercisePool> PoolEntries { get; set; } = new List<ExercisePool>();
}
