namespace MicroExercise.Core;

/// <summary>
/// Well-known constant values shared across layers. The demo user is seeded into
/// the database and auto-signed-in for the MVP (see spec §2, Authentication).
/// </summary>
public static class AppDefaults
{
    public const int DemoUserId = 1;
    public const string DemoUserEmail = "demo@microburst.local";
    public const string DemoUserDisplayName = "Demo User";
}
