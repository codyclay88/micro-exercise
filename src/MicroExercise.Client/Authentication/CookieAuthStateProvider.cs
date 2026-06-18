using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using MicroExercise.ApiClient;
using MicroExercise.Core.Dtos;

namespace MicroExercise.Client.Authentication;

/// <summary>
/// Establishes the WASM client's auth state from the server. The auth cookie is HttpOnly, so the
/// client can't read it directly; instead it calls <c>GET /api/auth/me</c> (which rides the cookie)
/// to learn who's signed in. Replaces the server-circuit revalidating provider.
/// </summary>
public class CookieAuthStateProvider(HttpClient http) : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var response = await http.GetAsync("api/auth/me");
            if (!response.IsSuccessStatusCode)
                return Anonymous;

            var me = await response.Content.ReadFromJsonAsync<CurrentUserDto>(ApiJson.Options);
            if (me is null)
                return Anonymous;

            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, me.UserId.ToString()),
                new Claim(ClaimTypes.Name, me.DisplayName)
            ], authenticationType: "cookie");

            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch (HttpRequestException)
        {
            return Anonymous;
        }
    }
}
