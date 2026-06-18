using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicroExercise.ApiClient;
using MicroExercise.Maui.Pages;

namespace MicroExercise.Maui.ViewModels;

/// <summary>
/// The History screen — a date-range list of bursts with edit (modal) and delete. Mirrors the web
/// <c>History.razor</c>: default last 7 days, reload whenever the range changes, and reload after a
/// mutation so timestamp-driven ordering stays correct.
/// </summary>
public partial class HistoryViewModel(LogApi log) : FeatureViewModel
{
    private bool _ready;

    public ObservableCollection<BurstRow> Bursts { get; } = [];

    [ObservableProperty] private DateTime _from = DateTime.Today.AddDays(-6);
    [ObservableProperty] private DateTime _to = DateTime.Today;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _countText = "";

    partial void OnFromChanged(DateTime value) { if (_ready) _ = LoadAsync(); }
    partial void OnToChanged(DateTime value) { if (_ready) _ = LoadAsync(); }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var from = DateOnly.FromDateTime(From);
            var to = DateOnly.FromDateTime(To);

            Bursts.Clear();
            if (to < from)
            {
                IsEmpty = true;
                CountText = "";
                return;
            }

            foreach (var burst in await log.GetHistoryAsync(from, to))
                Bursts.Add(new BurstRow(burst));

            IsEmpty = Bursts.Count == 0;
            CountText = IsEmpty ? "" : $"{Bursts.Count} burst{(Bursts.Count == 1 ? "" : "s")} shown.";
        }
        catch (Exception ex) when (IsConnectivityError(ex))
        {
            ErrorMessage = ConnectivityMessage;
        }
        finally
        {
            IsLoading = false;
            _ready = true;
        }
    }

    [RelayCommand]
    private async Task EditAsync(BurstRow? row)
    {
        if (row is null) return;
        var editor = new EditBurstViewModel(log, row.Burst);
        await Shell.Current.Navigation.PushModalAsync(new EditBurstPage(editor));
    }

    [RelayCommand]
    private async Task DeleteAsync(BurstRow? row)
    {
        if (row is null) return;
        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Delete burst?", $"{row.AmountText} · {row.ExerciseName}", "Delete", "Cancel");
        if (!confirmed) return;

        await log.DeleteLogAsync(row.Burst.Id);
        await LoadAsync();
    }
}
