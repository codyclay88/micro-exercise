using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicroExercise.ApiClient;

namespace MicroExercise.Maui.ViewModels;

/// <summary>
/// The Reports screen — a volume summary over a date range. Mirrors the web <c>Reports.razor</c>:
/// default last 30 days, 7/30/90-day presets, rows ordered most-performed first.
/// </summary>
public partial class ReportsViewModel(ReportApi report) : ObservableObject
{
    private bool _ready;

    public ObservableCollection<SummaryRow> Rows { get; } = [];

    [ObservableProperty] private DateTime _from = DateTime.Today.AddDays(-29);
    [ObservableProperty] private DateTime _to = DateTime.Today;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _headerText = "";

    partial void OnFromChanged(DateTime value) { if (_ready) _ = LoadAsync(); }
    partial void OnToChanged(DateTime value) { if (_ready) _ = LoadAsync(); }

    [RelayCommand]
    private async Task SetRangeAsync(string days)
    {
        if (!int.TryParse(days, out var span)) return;
        _ready = false;                       // adjust both ends, then load once
        To = DateTime.Today;
        From = DateTime.Today.AddDays(-(span - 1));
        _ready = true;
        await LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var from = DateOnly.FromDateTime(From);
            var to = DateOnly.FromDateTime(To);

            Rows.Clear();
            if (to < from)
            {
                IsEmpty = true;
                HeaderText = "";
                return;
            }

            var summary = await report.GetSummaryAsync(from, to);
            foreach (var row in summary
                         .OrderByDescending(s => s.TotalBursts)
                         .ThenBy(s => s.ExerciseName))
                Rows.Add(new SummaryRow(row));

            var totalBursts = summary.Sum(s => s.TotalBursts);
            IsEmpty = Rows.Count == 0;
            HeaderText = IsEmpty
                ? ""
                : $"{From.ToString("MMM d", CultureInfo.InvariantCulture)} – " +
                  $"{To.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)} · " +
                  $"{totalBursts} burst{(totalBursts == 1 ? "" : "s")} across " +
                  $"{Rows.Count} exercise{(Rows.Count == 1 ? "" : "s")}";
        }
        finally
        {
            IsLoading = false;
            _ready = true;
        }
    }
}
