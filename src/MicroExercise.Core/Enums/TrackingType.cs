namespace MicroExercise.Core.Enums;

/// <summary>
/// Determines how an exercise's quantity is measured and displayed.
/// </summary>
public enum TrackingType
{
    /// <summary>Quantity represents a number of repetitions (e.g. push-ups).</summary>
    Reps = 0,

    /// <summary>Quantity represents a duration in seconds (e.g. a dead hang).</summary>
    Seconds = 1
}
