using MicroExercise.Maui.Pages;
using MicroExercise.Maui.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MicroExercise.Maui;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly ISession _session;
    private readonly AuthService _auth;
    private Window? _window;

    /// <summary>Service provider for code-behind that can't take constructor injection (e.g. Shell menu handlers).</summary>
    public static IServiceProvider Services { get; private set; } = default!;

    public App(IServiceProvider services, ISession session, AuthService auth)
    {
        InitializeComponent();
        ThemePreference.Apply(this);   // restore the saved light/dark/system choice before any UI shows
        _services = services;
        _session = session;
        _auth = auth;
        Services = services;
        _session.Changed += OnSessionChanged;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _window = new Window(new LoadingPage());
        _ = StartAsync();
        return _window;
    }

    private async Task StartAsync()
    {
        // Restore a persisted cookie and verify it against /api/auth/me; the result drives the gate.
        await _auth.TryRestoreSessionAsync();
        ApplyRoot();
    }

    private void OnSessionChanged(object? sender, EventArgs e)
        => MainThread.BeginInvokeOnMainThread(ApplyRoot);

    private void ApplyRoot()
    {
        if (_window is null) return;
        _window.Page = _session.IsAuthenticated
            ? _services.GetRequiredService<AppShell>()
            : _services.GetRequiredService<LoginPage>();
    }
}
