namespace MicroExercise.Core.Dtos;

/// <summary>Confirmation of a written <c>WorkoutLog</c> returned by <c>POST /api/logs</c>.</summary>
public record LogResultDto(
    long Id, int ExercisePoolId, int CompletedQuantity, DateTimeOffset Timestamp, ResistanceDto Resistance);
