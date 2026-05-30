namespace MicroExercise.Core.Dtos;

/// <summary>Request body for <c>POST /api/logs</c> (spec §5).</summary>
public record CreateLogRequest(int ExercisePoolId, int Quantity);
