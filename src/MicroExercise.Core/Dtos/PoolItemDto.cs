using MicroExercise.Core.Enums;

namespace MicroExercise.Core.Dtos;

/// <summary>
/// A configured Quick-Log Card for the dashboard grid. <see cref="DisplayName"/> resolves
/// the custom override or falls back to the underlying exercise type name.
/// </summary>
public record PoolItemDto(
    int Id,
    int ExerciseTypeId,
    string DisplayName,
    TrackingType TrackingType,
    int LastQuantity,
    int SortOrder,
    bool IsActive);
