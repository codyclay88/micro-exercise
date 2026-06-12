using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicroExercise.ApiClient;
using MicroExercise.Core.Dtos;

namespace MicroExercise.Maui.ViewModels;

/// <summary>
/// The Log screen — the core daily loop. Mirrors the web <c>Dashboard.razor</c>: load the active
/// pool, seed each card with today's totals (the whole-day report summary), show active goals, and
/// log bursts with an optimistic local update of totals + goal progress.
/// </summary>
public partial class LogViewModel(PoolApi pool, LogApi log, ReportApi report, GoalApi goal) : ObservableObject
{
    public ObservableCollection<QuickLogItem> Pool { get; } = [];
    public ObservableCollection<GoalProgress> Goals { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _hasGoals;
    [ObservableProperty] private bool _isEmpty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalBurstsText))]
    private int _totalBurstsToday;

    public string TotalBurstsText => $"{TotalBurstsToday} bursts logged today";

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var items = await pool.GetActivePoolAsync();
            Pool.Clear();
            var byId = new Dictionary<int, QuickLogItem>();
            foreach (var item in items)
            {
                var card = new QuickLogItem(item);
                Pool.Add(card);
                byId[item.Id] = card;
            }

            // Seed each card with what's already been logged today (whole-day summary).
            var today = DateOnly.FromDateTime(DateTime.Today);
            var summary = await report.GetSummaryAsync(today, today);
            foreach (var row in summary)
                if (byId.TryGetValue(row.ExercisePoolId, out var card))
                    card.Seed(row.TotalBursts, row.TotalVolume);
            TotalBurstsToday = summary.Sum(s => s.TotalBursts);

            Goals.Clear();
            foreach (var g in await goal.GetGoalsAsync(includeCompleted: false))
                Goals.Add(new GoalProgress(g));

            HasGoals = Goals.Count > 0;
            IsEmpty = Pool.Count == 0;
            IsLoaded = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LogAsync(QuickLogItem? item)
    {
        if (item is null) return;

        var quantity = item.Pending;
        var result = await log.LogAsync(new CreateLogRequest(item.Id, quantity));
        if (result is null) return;   // pool item not owned/active — nothing to update

        item.RecordBurst(quantity);
        TotalBurstsToday++;

        // Advance any active goal on this exercise whose window contains now.
        var now = DateTimeOffset.Now;
        foreach (var g in Goals)
            if (g.ExercisePoolId == item.Id && now >= g.StartDate && now <= g.Deadline)
                g.Advance(quantity);
    }
}
