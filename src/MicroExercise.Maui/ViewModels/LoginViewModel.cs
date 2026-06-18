using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicroExercise.Maui.Services;

namespace MicroExercise.Maui.ViewModels;

public partial class LoginViewModel(AuthService auth) : ObservableObject
{
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private bool _rememberMe = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    public bool IsNotBusy => !IsBusy;
    public bool HasError => !string.IsNullOrEmpty(Error);

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsBusy) return;
        Error = null;
        IsBusy = true;
        try
        {
            var result = await auth.LoginAsync(Email.Trim(), Password, RememberMe);
            if (!result.Success)
                Error = result.Error;
            // On success the session changes and App swaps the window root to the shell.
        }
        finally
        {
            IsBusy = false;
        }
    }
}
