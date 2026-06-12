using MicroExercise.Maui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MicroExercise.Maui.Pages;

public partial class GoalsPage : ContentPage
{
    private readonly GoalsViewModel _viewModel;

    public GoalsPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<GoalsViewModel>();
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.LoadCommand.CanExecute(null))
            _viewModel.LoadCommand.Execute(null);
    }
}
