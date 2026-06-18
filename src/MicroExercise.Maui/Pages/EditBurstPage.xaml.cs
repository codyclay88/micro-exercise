using MicroExercise.Maui.ViewModels;

namespace MicroExercise.Maui.Pages;

public partial class EditBurstPage : ContentPage
{
    public EditBurstPage(EditBurstViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
