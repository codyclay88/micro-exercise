using System.Net.Http.Json;
using MicroExercise.Core.Dtos;

namespace MicroExercise.ApiClient;

/// <summary>Client for the goals endpoints under <c>/api/goals</c>.</summary>
public class GoalApi(HttpClient http)
{
    public async Task<IReadOnlyList<GoalDto>> GetGoalsAsync(bool includeCompleted = true, CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<GoalDto>>(
               $"api/goals?includeCompleted={(includeCompleted ? "true" : "false")}", ApiJson.Options, ct) ?? [];

    /// <summary>Creates a goal. Returns the created goal, or null if the pool item isn't owned/active.</summary>
    public async Task<GoalDto?> CreateGoalAsync(CreateGoalRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/goals", request, ApiJson.Options, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<GoalDto>(ApiJson.Options, ct)
            : null;
    }

    public Task DeleteGoalAsync(int id, CancellationToken ct = default)
        => http.DeleteAsync($"api/goals/{id}", ct);
}
