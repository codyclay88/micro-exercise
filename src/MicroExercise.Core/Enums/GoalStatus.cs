namespace MicroExercise.Core.Enums;

/// <summary>
/// The state of a <see cref="Entities.Goal"/>, computed on read from progress vs. target
/// vs. the clock — it is never persisted.
/// </summary>
public enum GoalStatus
{
    /// <summary>Not yet achieved, and the deadline has not passed.</summary>
    Active = 0,

    /// <summary>Logged volume in the window reached or exceeded the target.</summary>
    Achieved = 1,

    /// <summary>The deadline passed without the target being met.</summary>
    Expired = 2
}
