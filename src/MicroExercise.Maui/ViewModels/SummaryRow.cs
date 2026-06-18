using MicroExercise.Core.Dtos;
using MicroExercise.Core.Enums;

namespace MicroExercise.Maui.ViewModels;

/// <summary>Read-only display projection of an <see cref="ExerciseSummaryDto"/> for the Reports table.</summary>
public sealed class SummaryRow(ExerciseSummaryDto summary)
{
    private string Unit => summary.TrackingType == TrackingType.Seconds ? "sec" : "reps";

    public string ExerciseName => summary.ExerciseName;
    public string BurstsText => summary.TotalBursts.ToString();
    public string TotalText => $"{summary.TotalVolume} {Unit}";

    public string AvgText =>
        $"{(summary.TotalBursts == 0 ? "0" : ((double)summary.TotalVolume / summary.TotalBursts).ToString("0.#"))} {Unit}";
}
