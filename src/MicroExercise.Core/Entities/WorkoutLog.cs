namespace MicroExercise.Core.Entities;

/// <summary>
/// A single recorded exercise burst — the core transactional record of the app.
/// </summary>
public class WorkoutLog
{
    public long Id { get; set; }

    public int ExercisePoolId { get; set; }
    public ExercisePool? ExercisePool { get; set; }

    /// <summary>When the burst was logged. Stored with offset for accurate cross-timezone dates.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>The precise reps or seconds performed in this single burst.</summary>
    public int CompletedQuantity { get; set; }
}
