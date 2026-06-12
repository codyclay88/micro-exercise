using MicroExercise.Maui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MicroExercise.Maui.Pages;

public partial class LogPage : ContentPage
{
    private readonly LogViewModel _viewModel;

    // The Shell instantiates tab pages via its DataTemplate (parameterless), so we pull the
    // view model from the app container rather than relying on constructor injection here.
    public LogPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<LogViewModel>();
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_viewModel.IsLoaded && _viewModel.LoadCommand.CanExecute(null))
            _viewModel.LoadCommand.Execute(null);
    }
}
