using MicroExercise.Core.Dtos;

namespace MicroExercise.Core.Abstractions;

/// <summary>Writes exercise burst logs (spec §4.1 one-click log, §5 POST /api/logs).</summary>
public interface ILogService
{
    /// <summary>
    /// Records a burst against one of the user's active pool entries. Returns the created
    /// log, or <c>null</c> if the pool item does not exist or is not owned by the user.
    /// </summary>
    Task<LogResultDto?> LogAsync(int userId, CreateLogRequest request, CancellationToken ct = default);
}
