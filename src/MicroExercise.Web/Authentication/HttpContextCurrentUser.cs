using System.Security.Claims;
using MicroExercise.Core;
using MicroExercise.Core.Abstractions;

namespace MicroExercise.Web.Authentication;

/// <summary>
/// Resolves the current user from the authenticated cookie principal. Until real
/// registration/login exists, an unauthenticated request falls back to the seeded
/// demo user (spec §2, Authentication — MVP scope).
/// </summary>
public class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public int UserId
    {
        get
        {
            var claim = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out var id) ? id : AppDefaults.DemoUserId;
        }
    }
}
