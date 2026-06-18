using MicroExercise.Core.Dtos;
using MicroExercise.Core.Enums;

namespace MicroExercise.Maui.ViewModels;

/// <summary>Read-only display projection of a <see cref="BurstDto"/> for the History list.</summary>
public sealed class BurstRow(BurstDto burst)
{
    public BurstDto Burst => burst;
    public string ExerciseName => burst.ExerciseName;
    public string WhenText => burst.Timestamp.LocalDateTime.ToString("MMM d, h:mm tt");

    public string AmountText
    {
        get
        {
            var amount = $"{burst.Quantity} {(burst.TrackingType == TrackingType.Seconds ? "sec" : "reps")}";
            return burst.Resistance.IsBodyweight ? amount : $"{amount} · {burst.Resistance.Describe()}";
        }
    }
}
