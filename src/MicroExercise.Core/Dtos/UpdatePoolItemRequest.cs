namespace MicroExercise.Core.Dtos;

/// <summary>Request body for <c>PUT /api/exercises/pool/{id}</c> — customize a pool entry.</summary>
public record UpdatePoolItemRequest(string? CustomName, int TargetQuantity);
