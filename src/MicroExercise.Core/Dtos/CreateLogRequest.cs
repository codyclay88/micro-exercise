namespace MicroExercise.Core.Dtos;

/// <summary>
/// Request body for <c>POST /api/logs</c> (spec §5). <see cref="Resistance"/> is optional —
/// omitting it (null) records a plain bodyweight burst.
/// </summary>
public record CreateLogRequest(int ExercisePoolId, int Quantity, ResistanceDto? Resistance = null);
