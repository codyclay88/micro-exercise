namespace MicroExercise.Core.Dtos;

/// <summary>Request body for <c>POST /api/exercises/pool</c> (spec §5).</summary>
public record CreatePoolItemRequest(int ExerciseTypeId, int TargetQuantity, string? CustomName);
