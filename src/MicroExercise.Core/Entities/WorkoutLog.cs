using MicroExercise.Core.Enums;

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

    /// <summary>
    /// How the burst's added resistance is expressed. Defaults to <see cref="ResistanceType.Bodyweight"/>
    /// (no added load). See the resistance fields below for the type-specific payload.
    /// </summary>
    public ResistanceType ResistanceType { get; set; } = ResistanceType.Bodyweight;

    /// <summary>Numeric load for a <see cref="ResistanceType.Weight"/> burst (paired with <see cref="WeightUnit"/>); null otherwise.</summary>
    public decimal? ResistanceAmount { get; set; }

    /// <summary>Unit for <see cref="ResistanceAmount"/> (lbs/kg) on a <see cref="ResistanceType.Weight"/> burst; null otherwise.</summary>
    public WeightUnit? WeightUnit { get; set; }

    /// <summary>Free-text colour/label for a <see cref="ResistanceType.Band"/> burst (e.g. "Green"); null otherwise.</summary>
    public string? BandLabel { get; set; }
}
