using CommunityToolkit.Mvvm.ComponentModel;

namespace MicroExercise.Maui.ViewModels;

/// <summary>
/// Base for the tab screens: a shared error slot so an unreachable server surfaces a banner instead
/// of faulting the async load command. Each screen keeps its own <c>IsLoading</c> (so per-screen
/// computed enables/visibilities can react to it).
/// </summary>
public abstract partial class FeatureViewModel : ObservableObject
{
    protected const string ConnectivityMessage = "Couldn't reach the server. Pull down to retry.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>True for the network/parse failures we treat as "couldn't reach the server".</summary>
    protected static bool IsConnectivityError(Exception ex)
        => ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException;
}
