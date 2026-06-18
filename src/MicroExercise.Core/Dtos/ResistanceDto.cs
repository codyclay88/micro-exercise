using MicroExercise.Core.Enums;

namespace MicroExercise.Core.Dtos;

/// <summary>
/// The optional resistance recorded against a single burst. A small value object carried on the
/// log request/result and history DTOs. The shape is interpreted by <see cref="Type"/>:
/// <list type="bullet">
///   <item><see cref="ResistanceType.Bodyweight"/> — no added load; all other fields null.</item>
///   <item><see cref="ResistanceType.Weight"/> — <see cref="Amount"/> + <see cref="Unit"/> set
///   (e.g. 15 lbs of dumbbells).</item>
///   <item><see cref="ResistanceType.Band"/> — <see cref="BandLabel"/> set (e.g. "Green").</item>
/// </list>
/// </summary>
public record ResistanceDto(
    ResistanceType Type,
    decimal? Amount = null,
    WeightUnit? Unit = null,
    string? BandLabel = null)
{
    /// <summary>The default "no added load" resistance.</summary>
    public static readonly ResistanceDto Bodyweight = new(ResistanceType.Bodyweight);

    /// <summary><c>true</c> when this is plain bodyweight (the common case — usually hidden in the UI).</summary>
    public bool IsBodyweight => Type == ResistanceType.Bodyweight;

    /// <summary>
    /// A short human label for display, identical across the web and MAUI read paths:
    /// "15 lbs", "Green band", or "Bodyweight". Trailing decimal zeros are trimmed.
    /// </summary>
    public string Describe() => Type switch
    {
        ResistanceType.Weight when Amount is { } a =>
            $"{a.ToString(a % 1 == 0 ? "0" : "0.##")} {(Unit == WeightUnit.Kilograms ? "kg" : "lbs")}",
        ResistanceType.Band when !string.IsNullOrWhiteSpace(BandLabel) =>
            $"{BandLabel} band",
        _ => "Bodyweight"
    };
}
