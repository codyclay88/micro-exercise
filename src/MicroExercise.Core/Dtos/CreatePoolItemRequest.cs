namespace MicroExercise.Core.Dtos;

/// <summary>
/// Request body for <c>POST /api/exercises/pool</c> (spec §5). <see cref="LastQuantity"/> seeds
/// the pool item's "last amount" (the dialog pre-fill) until the first burst overwrites it.
/// </summary>
public record CreatePoolItemRequest(int ExerciseTypeId, int LastQuantity, string? CustomName);
