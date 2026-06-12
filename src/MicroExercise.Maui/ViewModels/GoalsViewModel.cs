using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicroExercise.ApiClient;
using MicroExercise.Core.Dtos;
using MicroExercise.Core.Enums;

namespace MicroExercise.Maui.ViewModels;

/// <summary>Dropdown option for the goal-create exercise picker (a pool item + a unit-qualified label).</summary>
public sealed class GoalPoolOption(PoolItemDto item)
{
    public int Id => item.Id;
    public string Label => $"{item.DisplayName} ({(item.TrackingType == TrackingType.Seconds ? "sec" : "reps")})";
}

/// <summary>
/// The Goals screen — set one-shot, deadline-bound targets and track them. Mirrors the web
/// <c>Goals.razor</c>: a create form over the active pool, then active goals (soonest deadline
/// first) and a completed/expired history section.
/// </summary>
public partial class GoalsViewModel(GoalApi goalApi, PoolApi poolApi) : ObservableObject
{
    public ObservableCollection<GoalPoolOption> PoolOptions { get; } = [];
    public ObservableCollection<GoalRow> ActiveGoals { get; } = [];
    public ObservableCollection<GoalRow> DoneGoals { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoPool))]
    private bool _hasPool;

    [ObservableProperty] private bool _hasDone;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoGoals))]
    private bool _hasGoals;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    private GoalPoolOption? _selectedPool;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    private int _target = 50;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    [NotifyPropertyChangedFor(nameof(DeadlineInvalid))]
    private DateTime _start = DateTime.Today;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    [NotifyPropertyChangedFor(nameof(DeadlineInvalid))]
    private DateTime _deadline = DateTime.Today.AddDays(3);

    public bool NoPool => !HasPool;
    public bool NoGoals => !HasGoals;
    public bool DeadlineInvalid => Deadline < Start;
    public bool CanCreate => Target > 0 && !DeadlineInvalid && SelectedPool is not null && !IsLoading;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var previousId = SelectedPool?.Id;
            var pool = await poolApi.GetActivePoolAsync();
            PoolOptions.Clear();
            foreach (var item in pool)
                PoolOptions.Add(new GoalPoolOption(item));
            HasPool = PoolOptions.Count > 0;
            SelectedPool = PoolOptions.FirstOrDefault(o => o.Id == previousId) ?? PoolOptions.FirstOrDefault();

            // Active first (soonest deadline first); completed/expired after (most recent first).
            var goals = (await goalApi.GetGoalsAsync(includeCompleted: true))
                .OrderBy(g => g.Status == GoalStatus.Active ? 0 : 1)
                .ThenBy(g => g.Status == GoalStatus.Active ? g.Deadline : DateTimeOffset.MaxValue)
                .ThenByDescending(g => g.Deadline)
                .ToList();

            ActiveGoals.Clear();
            DoneGoals.Clear();
            foreach (var g in goals)
            {
                if (g.Status == GoalStatus.Active) ActiveGoals.Add(new GoalRow(g));
                else DoneGoals.Add(new GoalRow(g));
            }
            HasGoals = goals.Count > 0;
            HasDone = DoneGoals.Count > 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (!CanCreate) return;
        IsLoading = true;
        try
        {
            var start = new DateTimeOffset(Start.Date);
            var deadline = new DateTimeOffset(Deadline.Date.AddDays(1).AddSeconds(-1)); // end of the day
            await goalApi.CreateGoalAsync(new CreateGoalRequest(SelectedPool!.Id, Target, deadline, start));
            Target = 50;
        }
        finally
        {
            IsLoading = false;
        }
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(GoalRow? row)
    {
        if (row is null) return;
        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Delete goal?", $"{row.ExerciseName} · {row.ProgressText}", "Delete", "Cancel");
        if (!confirmed) return;

        await goalApi.DeleteGoalAsync(row.Id);
        await LoadAsync();
    }
}
