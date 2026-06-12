using CommunityToolkit.Mvvm.ComponentModel;
using MicroExercise.Core.Dtos;
using MicroExercise.Core.Enums;

namespace MicroExercise.Maui.ViewModels;

/// <summary>
/// An active goal's live progress strip on the Log screen. <see cref="Advance"/> recomputes the
/// derived fields client-side so the bar moves the instant a burst is logged, without a round-trip
/// (mirrors the web Dashboard's optimistic goal update). An active goal can only cross into Achieved.
/// </summary>
public partial class GoalProgress : ObservableObject
{
    private readonly GoalDto _goal;

    public GoalProgress(GoalDto goal)
    {
        _goal = goal;
        CurrentProgress = goal.CurrentProgress;
        Percent = ComputePercent(goal.CurrentProgress);
        IsAchieved = goal.Status == GoalStatus.Achieved;
    }

    public int ExercisePoolId => _goal.ExercisePoolId;
    public DateTimeOffset StartDate => _goal.StartDate;
    public DateTimeOffset Deadline => _goal.Deadline;
    public string ExerciseName => _goal.ExerciseName;
    public string Unit => _goal.TrackingType == TrackingType.Seconds ? "sec" : "reps";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private int _currentProgress;

    [ObservableProperty] private double _percent;   // 0..1 for the ProgressBar
    [ObservableProperty] private bool _isAchieved;

    public string ProgressText => $"{CurrentProgress} / {_goal.TargetQuantity} {Unit}";

    public string DeadlineText => (Deadline.LocalDateTime.Date - DateTime.Today).Days switch
    {
        < 0 => "overdue",
        0 => "due today",
        1 => "1 day left",
        var d => $"{d} days left",
    };

    public void Advance(int quantity)
    {
        CurrentProgress += quantity;
        Percent = ComputePercent(CurrentProgress);
        if (CurrentProgress >= _goal.TargetQuantity)
            IsAchieved = true;
    }

    private double ComputePercent(int progress)
        => _goal.TargetQuantity <= 0 ? 0 : Math.Min(1.0, (double)progress / _goal.TargetQuantity);
}
