using System.Globalization;
using MicroExercise.Core.Dtos;
using MicroExercise.Core.Enums;

namespace MicroExercise.Maui.ViewModels;

/// <summary>Read-only display projection of a <see cref="GoalDto"/> for the Goals list.</summary>
public sealed class GoalRow(GoalDto goal)
{
    private string Unit => goal.TrackingType == TrackingType.Seconds ? "sec" : "reps";

    public int Id => goal.Id;
    public string ExerciseName => goal.ExerciseName;
    public string ProgressText => $"{goal.CurrentProgress} / {goal.TargetQuantity} {Unit}";
    public string StatusText => goal.Status.ToString();
    public double Percent => Math.Min(1.0, goal.PercentComplete / 100.0);

    public string DetailText
    {
        get
        {
            var percent = goal.PercentComplete.ToString("0.#", CultureInfo.InvariantCulture) + "%";
            return goal.Status == GoalStatus.Active
                ? $"{percent} · {goal.RemainingQuantity} {Unit} to go"
                : percent;
        }
    }

    public string DeadlineText
    {
        get
        {
            if (goal.Status != GoalStatus.Active)
                return $"by {goal.Deadline.LocalDateTime:MMM d}";

            return (goal.Deadline.LocalDateTime.Date - DateTime.Today).Days switch
            {
                < 0 => "overdue",
                0 => "due today",
                1 => "1 day left",
                var d => $"{d} days left",
            };
        }
    }

    public Color StatusColor => goal.Status switch
    {
        GoalStatus.Achieved => Color.FromArgb("#198754"),
        GoalStatus.Expired => Color.FromArgb("#6C757D"),
        _ => Color.FromArgb("#0D6EFD"),
    };
}
