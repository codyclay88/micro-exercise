namespace MicroExercise.Maui.Pages;

/// <summary>
/// Temporary tab content. Each screen gets its real XAML + ViewModel in its phase
/// (Log/History/Reports -> done, Goals/Pool -> Phase 4).
/// </summary>
public abstract class PlaceholderPage : ContentPage
{
    protected PlaceholderPage(string title, string note)
    {
        Title = title;
        Content = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = 24,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = title, FontSize = 28, HorizontalOptions = LayoutOptions.Center },
                new Label { Text = note, Opacity = 0.6, HorizontalOptions = LayoutOptions.Center },
            },
        };
    }
}

public sealed class GoalsPage() : PlaceholderPage("Goals", "Coming in Phase 4");
public sealed class PoolPage() : PlaceholderPage("Pool", "Coming in Phase 4");
