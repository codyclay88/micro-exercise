namespace MicroExercise.Core.Entities;

/// <summary>
/// A user's one-shot, deadline-bound target against one of their <see cref="ExercisePool"/>
/// entries (e.g. "100 push-ups by Friday"). Progress is derived from <see cref="WorkoutLog"/>
/// bursts in the [<see cref="StartDate"/>, <see cref="Deadline"/>] window — nothing about
/// progress or status is stored on the goal itself.
/// </summary>
public class Goal
{
    public int Id { get; set; }

    /// <summary>
    /// Owning user id (FK to the Identity user table, configured in Infrastructure). Stored
    /// directly — not only via the pool item — so per-user queries (and future per-challenge
    /// dedupe) don't need to join through <see cref="ExercisePool"/>.
    /// </summary>
    public int UserId { get; set; }

    public int ExercisePoolId { get; set; }
    public ExercisePool? ExercisePool { get; set; }

    /// <summary>Target volume to reach within the window, in the pool item's unit (reps or seconds).</summary>
    public int TargetQuantity { get; set; }

    /// <summary>Window start; bursts on/after this instant count. May be backdated at creation.</summary>
    public DateTimeOffset StartDate { get; set; }

    /// <summary>Window end; bursts on/before this instant count.</summary>
    public DateTimeOffset Deadline { get; set; }

    /// <summary>When the goal was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
