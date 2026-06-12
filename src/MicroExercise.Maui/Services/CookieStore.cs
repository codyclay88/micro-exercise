using System.Net;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace MicroExercise.Maui.Services;

/// <summary>
/// Persists the Identity auth cookie to <see cref="SecureStorage"/> so the session survives app
/// restarts (the cookie is 30-day sliding). Only the Identity cookie is kept; the transient
/// antiforgery cookie is discarded. Shares the one <see cref="CookieContainer"/> the HttpClient uses.
/// </summary>
public sealed class CookieStore(CookieContainer container, BackendOptions backend)
{
    private const string StorageKey = "auth.identity.cookie";
    private const string IdentityCookieName = ".AspNetCore.Identity.Application";

    /// <summary>Rehydrate the persisted cookie into the shared container, if present.</summary>
    public async Task LoadAsync()
    {
        var json = await SecureStorage.Default.GetAsync(StorageKey);
        if (string.IsNullOrEmpty(json)) return;

        var saved = JsonSerializer.Deserialize<SavedCookie>(json);
        if (saved is null) return;

        container.Add(backend.BaseAddress, new Cookie(saved.Name, saved.Value, saved.Path, backend.BaseAddress.Host)
        {
            HttpOnly = true,
            Secure = backend.BaseAddress.Scheme == Uri.UriSchemeHttps,
        });
    }

    /// <summary>Persist the current Identity cookie from the container (call after a successful login).</summary>
    public async Task SaveAsync()
    {
        var cookie = container.GetCookies(backend.BaseAddress)
            .FirstOrDefault(c => c.Name == IdentityCookieName);
        if (cookie is null) return;

        var json = JsonSerializer.Serialize(new SavedCookie(cookie.Name, cookie.Value, cookie.Path));
        await SecureStorage.Default.SetAsync(StorageKey, json);
    }

    /// <summary>Expire the container's cookies and forget the persisted one (logout).</summary>
    public Task ClearAsync()
    {
        foreach (Cookie cookie in container.GetCookies(backend.BaseAddress))
            cookie.Expired = true;
        SecureStorage.Default.Remove(StorageKey);
        return Task.CompletedTask;
    }

    private sealed record SavedCookie(string Name, string Value, string Path);
}
