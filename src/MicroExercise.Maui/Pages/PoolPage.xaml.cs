using MicroExercise.Maui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MicroExercise.Maui.Pages;

public partial class PoolPage : ContentPage
{
    private readonly PoolViewModel _viewModel;

    public PoolPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<PoolViewModel>();
        BindingContext = _viewModel;
    }

    // Reload on every appearance, including returning from the edit modal.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.LoadCommand.CanExecute(null))
            _viewModel.LoadCommand.Execute(null);
    }
}
