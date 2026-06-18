namespace MicroExercise.Core.Enums;

/// <summary>
/// How the resistance for a single burst is expressed. Most bodyweight movements are
/// <see cref="Bodyweight"/> (no added load); weighted equipment (dumbbells, kettlebells,
/// barbells, sandbags) records a numeric <see cref="Weight"/>; resistance bands use a
/// free-text colour/label (<see cref="Band"/>) because their load isn't reliably known.
/// </summary>
public enum ResistanceType
{
    /// <summary>No added load — the movement is performed at bodyweight (resistance of 0).</summary>
    Bodyweight = 0,

    /// <summary>A numeric load with a unit, e.g. 15 lbs of dumbbells.</summary>
    Weight = 1,

    /// <summary>A resistance band identified by a free-text colour/label, e.g. "Green".</summary>
    Band = 2
}
