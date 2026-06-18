using MicroExercise.Maui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MicroExercise.Maui.Pages;

public partial class ReportsPage : ContentPage
{
    private readonly ReportsViewModel _viewModel;

    public ReportsPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<ReportsViewModel>();
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.LoadCommand.CanExecute(null))
            _viewModel.LoadCommand.Execute(null);
    }
}
