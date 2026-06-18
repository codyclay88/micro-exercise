using Microsoft.Maui.Storage;

namespace MicroExercise.Maui.Services;

/// <summary>
/// Persists the user's light/dark/system theme choice (Preferences) and applies it via
/// <see cref="Application.UserAppTheme"/>. Mirrors the web app's theme toggle; "System" defers to
/// the OS setting. Built-in styles use <c>AppThemeBinding</c>, so changing the theme re-skins live.
/// </summary>
public static class ThemePreference
{
    private const string Key = "app.theme";

    public static AppTheme Current => (AppTheme)Preferences.Default.Get(Key, (int)AppTheme.Unspecified);

    /// <summary>Apply the saved theme on startup (call before the first page shows).</summary>
    public static void Apply(Application app) => app.UserAppTheme = Current;

    /// <summary>Cycle System → Light → Dark → System, persist, and apply. Returns the new value.</summary>
    public static AppTheme Cycle()
    {
        var next = Current switch
        {
            AppTheme.Unspecified => AppTheme.Light,
            AppTheme.Light => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };
        Preferences.Default.Set(Key, (int)next);
        if (Application.Current is { } app)
            app.UserAppTheme = next;
        return next;
    }

    public static string MenuLabel(AppTheme theme) => theme switch
    {
        AppTheme.Light => "Theme: Light",
        AppTheme.Dark => "Theme: Dark",
        _ => "Theme: System",
    };
}
