namespace MicroExercise.Core.Enums;

/// <summary>
/// The unit a weighted <see cref="ResistanceType.Weight"/> resistance is recorded in. Stored
/// per burst (not a single account-wide setting) so a user can mix lbs and kg freely.
/// </summary>
public enum WeightUnit
{
    /// <summary>Pounds (lbs).</summary>
    Pounds = 0,

    /// <summary>Kilograms (kg).</summary>
    Kilograms = 1
}
