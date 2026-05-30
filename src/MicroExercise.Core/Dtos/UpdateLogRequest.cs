namespace MicroExercise.Core.Dtos;

/// <summary>Request body for <c>PUT /api/logs/{id}</c> — corrects a recorded burst.</summary>
public record UpdateLogRequest(int Quantity, DateTimeOffset Timestamp);
