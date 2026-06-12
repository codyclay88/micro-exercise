namespace MicroExercise.Maui.Pages;

/// <summary>
/// Temporary tab content for Phase 1. Each screen gets its real XAML + ViewModel in later phases
/// (Log -> Phase 2, History/Reports -> Phase 3, Goals/Pool -> Phase 4).
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

public sealed class LogPage() : PlaceholderPage("Log", "Coming in Phase 2");
public sealed class HistoryPage() : PlaceholderPage("History", "Coming in Phase 3");
public sealed class ReportsPage() : PlaceholderPage("Reports", "Coming in Phase 3");
public sealed class GoalsPage() : PlaceholderPage("Goals", "Coming in Phase 4");
public sealed class PoolPage() : PlaceholderPage("Pool", "Coming in Phase 4");
