using MicroExercise.Maui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MicroExercise.Maui.Pages;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _viewModel;

    public HistoryPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<HistoryViewModel>();
        BindingContext = _viewModel;
    }

    // Reload on every appearance — including returning from the edit modal, where a timestamp
    // change may have reordered the list.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.LoadCommand.CanExecute(null))
            _viewModel.LoadCommand.Execute(null);
    }
}
