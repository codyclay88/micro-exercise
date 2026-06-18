using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicroExercise.ApiClient;
using MicroExercise.Core.Dtos;
using MicroExercise.Core.Enums;

namespace MicroExercise.Maui.ViewModels;

/// <summary>
/// Backs the modal pool-item editor: set an optional custom name (overriding the type name) and the
/// last amount, then PUT /api/exercises/pool/{id}. The Pool list refreshes when the modal is dismissed.
/// </summary>
public partial class EditPoolItemViewModel : ObservableObject
{
    private readonly PoolApi _poolApi;
    private readonly PoolItemDto _item;

    public EditPoolItemViewModel(PoolApi poolApi, PoolItemDto item, string typeName)
    {
        _poolApi = poolApi;
        _item = item;
        TypeName = typeName;
        // Prefill the current override, blank when the item just uses the type name.
        CustomName = string.Equals(item.DisplayName, typeName, StringComparison.Ordinal) ? null : item.DisplayName;
        LastAmount = item.LastQuantity;
    }

    public string TypeName { get; }
    public string Unit => _item.TrackingType == TrackingType.Seconds ? "sec" : "reps";

    [ObservableProperty] private string? _customName;
    [ObservableProperty] private int _lastAmount;
    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy || LastAmount <= 0) return;
        IsBusy = true;
        try
        {
            var name = string.IsNullOrWhiteSpace(CustomName) ? null : CustomName.Trim();
            await _poolApi.UpdatePoolItemAsync(_item.Id, new UpdatePoolItemRequest(name, LastAmount));
            await Shell.Current.Navigation.PopModalAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static Task CancelAsync() => Shell.Current.Navigation.PopModalAsync();
}
