using MicroExercise.Maui.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MicroExercise.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        ThemeMenuItem.Text = ThemePreference.MenuLabel(ThemePreference.Current);
    }

    private void OnToggleTheme(object? sender, EventArgs e)
        => ThemeMenuItem.Text = ThemePreference.MenuLabel(ThemePreference.Cycle());

    private async void OnSignOut(object? sender, EventArgs e)
        => await App.Services.GetRequiredService<AuthService>().LogoutAsync();
}
