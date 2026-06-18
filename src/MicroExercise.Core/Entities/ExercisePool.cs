namespace MicroExercise.Core.Entities;

/// <summary>
/// A user's customized instance of an <see cref="ExerciseType"/> — the unit that
/// renders as a Quick-Log Card on the dashboard. Soft-deleted via <see cref="IsActive"/>
/// so historical <see cref="WorkoutLog"/> data is never orphaned.
/// </summary>
public class ExercisePool
{
    public int Id { get; set; }

    /// <summary>Owning user id (FK to the Identity user table, configured in Infrastructure).</summary>
    public int UserId { get; set; }

    public int ExerciseTypeId { get; set; }
    public ExerciseType? ExerciseType { get; set; }

    /// <summary>Optional display override, e.g. "KB RDL (35 lbs)". Falls back to the type name.</summary>
    public string? CustomName { get; set; }

    /// <summary>
    /// The amount performed on the most recent burst — pre-fills the log dialog (your next burst
    /// is usually similar to your last). Seeded when the pool item is created and overwritten on
    /// each new burst; overtyped per set (not a cap).
    /// </summary>
    public int LastQuantity { get; set; }

    /// <summary>Soft-delete flag; inactive entries leave the grid but retain their history.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Ascending display priority for the dashboard grid (lower sorts first).</summary>
    public int SortOrder { get; set; }

    /// <summary>The individual burst logs recorded against this pool entry.</summary>
    public ICollection<WorkoutLog> Logs { get; set; } = new List<WorkoutLog>();
}
