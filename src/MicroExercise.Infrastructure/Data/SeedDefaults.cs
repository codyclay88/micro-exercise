namespace MicroExercise.Infrastructure.Data;

/// <summary>
/// Fixed values used by <c>HasData</c> seeding. A constant timestamp keeps EF Core
/// migrations deterministic (a dynamic value would mark the model as always-changed).
/// </summary>
internal static class SeedDefaults
{
    public static readonly DateTimeOffset Timestamp =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
