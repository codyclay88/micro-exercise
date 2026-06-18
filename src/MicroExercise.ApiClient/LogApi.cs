using System.Net.Http.Json;
using MicroExercise.Core.Dtos;

namespace MicroExercise.ApiClient;

/// <summary>Client for the burst-log endpoints under <c>/api/logs</c>.</summary>
public class LogApi(HttpClient http)
{
    /// <summary>Records a burst. Returns the written log, or null if the pool item isn't owned/active.</summary>
    public async Task<LogResultDto?> LogAsync(CreateLogRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/logs", request, ApiJson.Options, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<LogResultDto>(ApiJson.Options, ct)
            : null;
    }

    public async Task<IReadOnlyList<BurstDto>> GetHistoryAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<BurstDto>>(
               $"api/logs?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", ApiJson.Options, ct) ?? [];

    public Task UpdateLogAsync(long id, UpdateLogRequest request, CancellationToken ct = default)
        => http.PutAsJsonAsync($"api/logs/{id}", request, ApiJson.Options, ct);

    public Task DeleteLogAsync(long id, CancellationToken ct = default)
        => http.DeleteAsync($"api/logs/{id}", ct);
}
