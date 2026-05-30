using MicroExercise.Core.Abstractions;

namespace MicroExercise.Web.Authentication;

/// <summary>
/// Resolves the current user id from the authenticated principal for HTTP API requests.
/// API endpoints require authorization, so a principal is always present here.
/// (Blazor components resolve the user from <c>AuthenticationState</c> instead, since
/// <c>HttpContext</c> is not available inside an interactive circuit.)
/// </summary>
public class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public int UserId =>
        accessor.HttpContext?.User.GetUserId()
        ?? throw new InvalidOperationException("No HttpContext is available.");
}
