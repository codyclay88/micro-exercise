using MicroExercise.Maui.ViewModels;

namespace MicroExercise.Maui.Pages;

public partial class EditPoolItemPage : ContentPage
{
    public EditPoolItemPage(EditPoolItemViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
