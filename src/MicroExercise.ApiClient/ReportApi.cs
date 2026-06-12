using System.Net.Http.Json;
using MicroExercise.Core.Dtos;

namespace MicroExercise.ApiClient;

/// <summary>Client for the reporting endpoint under <c>/api/reports</c>.</summary>
public class ReportApi(HttpClient http)
{
    public async Task<IReadOnlyList<ExerciseSummaryDto>> GetSummaryAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<ExerciseSummaryDto>>(
               $"api/reports/summary?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", ApiJson.Options, ct) ?? [];
}
