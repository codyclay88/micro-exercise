using MicroExercise.Core.Dtos;
using MicroExercise.Core.Enums;

namespace MicroExercise.Maui.ViewModels;

/// <summary>Read-only display projection of a <see cref="PoolItemDto"/> for the Pool list.</summary>
public sealed class PoolItemRow(PoolItemDto item, string typeName, bool isFirst, bool isLast)
{
    public PoolItemDto Item => item;
    public string TypeName => typeName;
    public int Id => item.Id;
    public string DisplayName => item.DisplayName;
    public bool CanMoveUp => !isFirst;
    public bool CanMoveDown => !isLast;

    public string DetailText
    {
        get
        {
            var unit = item.TrackingType == TrackingType.Seconds ? "sec" : "reps";
            var baseText = $"{item.TargetQuantity} {unit}";
            // Show the underlying type only when a custom name overrides it.
            return string.Equals(item.DisplayName, typeName, StringComparison.Ordinal)
                ? baseText
                : $"{baseText} · {typeName}";
        }
    }
}
