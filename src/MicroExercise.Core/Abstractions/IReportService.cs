using MicroExercise.Core.Dtos;

namespace MicroExercise.Core.Abstractions;

/// <summary>Date-range aggregation of burst history (spec §4.3 / §5.1).</summary>
public interface IReportService
{
    /// <summary>
    /// Summarizes the user's logs in the inclusive [<paramref name="start"/>, <paramref name="end"/>]
    /// window, grouped per pool item with total volume and burst count.
    /// </summary>
    Task<IReadOnlyList<ExerciseSummaryDto>> GetSummaryAsync(
        int userId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
}
