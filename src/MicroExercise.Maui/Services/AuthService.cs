using System.Net.Http.Json;
using System.Text.RegularExpressions;
using MicroExercise.ApiClient;
using MicroExercise.Core.Dtos;

namespace MicroExercise.Maui.Services;

public sealed record AuthResult(bool Success, string? Error)
{
    public static readonly AuthResult Ok = new(true, null);
    public static AuthResult Fail(string error) => new(false, error);
}

/// <summary>
/// Reuses the server's cookie auth from a native client — no bearer/JWT, no server changes
/// (docs/MAUI-Mobile-App-Design.md §4). Login drives the static-SSR <c>/login</c> form: GET it to
/// pick up the antiforgery cookie + token, then POST credentials form-urlencoded. The cookie jar
/// is the source of truth — success is confirmed by <c>GET /api/auth/me</c>.
/// </summary>
public sealed partial class AuthService(HttpClient http, ISession session, CookieStore cookies)
{
    /// <summary>Rehydrate a persisted cookie and confirm it against the server. True if still signed in.</summary>
    public async Task<bool> TryRestoreSessionAsync(CancellationToken ct = default)
    {
        await cookies.LoadAsync();
        return await RefreshAsync(ct);
    }

    /// <summary>Ask the server who we are (rides the cookie) and update the session.</summary>
    public async Task<bool> RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await http.GetAsync("api/auth/me", ct);
            if (!response.IsSuccessStatusCode)
            {
                session.Set(null);
                return false;
            }

            var me = await response.Content.ReadFromJsonAsync<CurrentUserDto>(ApiJson.Options, ct);
            session.Set(me);
            return me is not null;
        }
        catch (HttpRequestException)
        {
            session.Set(null);
            return false;
        }
    }

    public async Task<AuthResult> LoginAsync(string email, string password, bool rememberMe, CancellationToken ct = default)
    {
        string token;
        try
        {
            token = await GetAntiforgeryTokenAsync(ct);
        }
        catch (HttpRequestException)
        {
            return AuthResult.Fail("Couldn't reach the server.");
        }
        catch (InvalidOperationException)
        {
            return AuthResult.Fail("Unexpected login page from the server.");
        }

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.RememberMe"] = rememberMe ? "true" : "false",
            ["__RequestVerificationToken"] = token,
            ["_handler"] = "login",
        });

        try
        {
            // Success = a 302 back to the SPA (Identity cookie written into the jar); bad credentials
            // re-render the page (200). AllowAutoRedirect is off, so we don't fetch the SPA HTML.
            await http.PostAsync("login", form, ct);
        }
        catch (HttpRequestException)
        {
            return AuthResult.Fail("Couldn't reach the server.");
        }

        if (await RefreshAsync(ct))
        {
            await cookies.SaveAsync();
            return AuthResult.Ok;
        }

        return AuthResult.Fail("Invalid email or password.");
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        try
        {
            await http.PostAsync("account/logout", content: null, ct);
        }
        catch (HttpRequestException)
        {
            // Sign out locally regardless of whether the server call succeeded.
        }

        await cookies.ClearAsync();
        session.Set(null);
    }

    private async Task<string> GetAntiforgeryTokenAsync(CancellationToken ct)
    {
        var html = await http.GetStringAsync("login", ct);
        var match = AntiforgeryTokenRegex().Match(html);
        if (!match.Success)
            throw new InvalidOperationException("Antiforgery token not found on /login.");
        return match.Groups[1].Value;
    }

    // The Blazor static-SSR form renders <input ... name="__RequestVerificationToken" ... value="..." />.
    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*\\bvalue=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();
}
