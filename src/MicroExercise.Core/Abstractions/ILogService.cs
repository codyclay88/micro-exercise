using MicroExercise.Core.Dtos;

namespace MicroExercise.Core.Abstractions;

/// <summary>
/// Reads and writes individual exercise bursts (spec §4.1 one-click log, §4.4 history
/// editing, §5 logs endpoints). All operations are scoped to the owning user.
/// </summary>
public interface ILogService
{
    /// <summary>
    /// Records a burst against one of the user's active pool entries. Returns the created
    /// log, or <c>null</c> if the pool item does not exist or is not owned by the user.
    /// </summary>
    Task<LogResultDto?> LogAsync(int userId, CreateLogRequest request, CancellationToken ct = default);

    /// <summary>
    /// The user's individual bursts in the inclusive [<paramref name="start"/>,
    /// <paramref name="end"/>] window, most recent first.
    /// </summary>
    Task<IReadOnlyList<BurstDto>> GetHistoryAsync(
        int userId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);

    /// <summary>
    /// Corrects a burst's quantity and timestamp. Returns the updated burst, or <c>null</c>
    /// if the burst does not exist or is not owned by the user.
    /// </summary>
    Task<BurstDto?> UpdateLogAsync(
        int userId, long logId, UpdateLogRequest request, CancellationToken ct = default);

    /// <summary>
    /// Permanently deletes a burst. Returns <c>false</c> if it does not exist or is not
    /// owned by the user.
    /// </summary>
    Task<bool> DeleteLogAsync(int userId, long logId, CancellationToken ct = default);
}
