using MicroExercise.Maui.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MicroExercise.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
    }

    private async void OnSignOut(object? sender, EventArgs e)
        => await App.Services.GetRequiredService<AuthService>().LogoutAsync();
}
