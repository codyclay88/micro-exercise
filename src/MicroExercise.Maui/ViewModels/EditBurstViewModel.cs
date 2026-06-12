using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicroExercise.ApiClient;
using MicroExercise.Core.Dtos;
using MicroExercise.Core.Enums;

namespace MicroExercise.Maui.ViewModels;

/// <summary>
/// Backs the modal burst editor: correct a burst's quantity and timestamp, then PUT /api/logs/{id}.
/// The History list refreshes when the modal is dismissed (its page reappears). Timestamp edits can
/// reorder the list, which is why History reloads rather than patching in place — same as the web.
/// </summary>
public partial class EditBurstViewModel : ObservableObject
{
    private readonly LogApi _log;
    private readonly BurstDto _burst;

    public EditBurstViewModel(LogApi log, BurstDto burst)
    {
        _log = log;
        _burst = burst;
        Quantity = burst.Quantity;
        Date = burst.Timestamp.LocalDateTime.Date;
        Time = burst.Timestamp.LocalDateTime.TimeOfDay;
    }

    public string ExerciseName => _burst.ExerciseName;
    public string Unit => _burst.TrackingType == TrackingType.Seconds ? "sec" : "reps";

    [ObservableProperty] private int _quantity;
    [ObservableProperty] private DateTime _date;
    [ObservableProperty] private TimeSpan _time;
    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy || Quantity <= 0) return;
        IsBusy = true;
        try
        {
            var timestamp = new DateTimeOffset(Date.Date + Time);
            await _log.UpdateLogAsync(_burst.Id, new UpdateLogRequest(Quantity, timestamp));
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
