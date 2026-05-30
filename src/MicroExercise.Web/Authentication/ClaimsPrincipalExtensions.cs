using System.Security.Claims;

namespace MicroExercise.Web.Authentication;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Reads the integer user id from the Identity <c>NameIdentifier</c> claim.</summary>
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id)
            ? id
            : throw new InvalidOperationException("No authenticated user id was found.");
    }
}
