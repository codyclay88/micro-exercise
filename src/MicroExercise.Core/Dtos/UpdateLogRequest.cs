namespace MicroExercise.Core.Dtos;

/// <summary>
/// Request body for <c>PUT /api/logs/{id}</c> — corrects a recorded burst. Pass the burst's
/// existing <see cref="Resistance"/> to preserve it; null records bodyweight.
/// </summary>
public record UpdateLogRequest(int Quantity, DateTimeOffset Timestamp, ResistanceDto? Resistance = null);
