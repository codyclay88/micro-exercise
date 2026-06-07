namespace MicroExercise.Core.Dtos;

/// <summary>
/// The authenticated user's identity, returned by <c>GET /api/auth/me</c> so the WASM client
/// can establish its <see cref="System.Security.Claims.ClaimsPrincipal"/> without access to the
/// HttpOnly auth cookie.
/// </summary>
public record CurrentUserDto(int UserId, string DisplayName, string? Email);
