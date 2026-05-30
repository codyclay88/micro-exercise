using Microsoft.AspNetCore.Identity;

namespace MicroExercise.Infrastructure.Identity;

/// <summary>
/// The application user, backed by ASP.NET Core Identity with integer keys (matching the
/// rest of the schema). Persistence concern — lives in Infrastructure so Core stays
/// framework-free; domain code references users by <c>int UserId</c> only.
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    /// <summary>Friendly name shown in the UI. Falls back to the email local-part if unset.</summary>
    public string? DisplayName { get; set; }
}
