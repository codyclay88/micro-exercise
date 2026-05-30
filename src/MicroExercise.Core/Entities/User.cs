namespace MicroExercise.Core.Entities;

/// <summary>
/// A minimal application user. For the MVP a single demo user is seeded and
/// auto-signed-in; this entity is structured so a full identity provider can
/// be layered on later without changing the dependent schema.
/// </summary>
public class User
{
    public int Id { get; set; }

    /// <summary>Login / contact email. Unique per user.</summary>
    public required string Email { get; set; }

    /// <summary>Friendly name shown in the UI.</summary>
    public required string DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The exercises this user has configured for their quick-log grid.</summary>
    public ICollection<ExercisePool> Pool { get; set; } = new List<ExercisePool>();
}
