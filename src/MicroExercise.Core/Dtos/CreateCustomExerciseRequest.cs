using MicroExercise.Core.Enums;

namespace MicroExercise.Core.Dtos;

/// <summary>
/// Request to create a brand-new custom exercise and add it to the user's pool in one step
/// (spec §4.2). The exercise becomes reusable in the owner's catalog.
/// </summary>
public record CreateCustomExerciseRequest(string Name, TrackingType TrackingType, int LastQuantity);
