namespace MicroExercise.Maui.Pages;

/// <summary>Shown while the app hydrates the session on launch (before the auth gate decides).</summary>
public sealed class LoadingPage : ContentPage
{
    public LoadingPage()
    {
        Content = new ActivityIndicator
        {
            IsRunning = true,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
        };
    }
}
