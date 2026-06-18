using System.Net.Http.Json;
using MicroExercise.Core.Dtos;

namespace MicroExercise.ApiClient;

/// <summary>Client for the exercise-pool endpoints under <c>/api/exercises</c>.</summary>
public class PoolApi(HttpClient http)
{
    public async Task<IReadOnlyList<PoolItemDto>> GetActivePoolAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<PoolItemDto>>("api/exercises/pool", ApiJson.Options, ct) ?? [];

    public async Task<IReadOnlyList<ExerciseTypeDto>> GetExerciseTypesAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<List<ExerciseTypeDto>>("api/exercises/types", ApiJson.Options, ct) ?? [];

    public Task AddPoolItemAsync(CreatePoolItemRequest request, CancellationToken ct = default)
        => http.PostAsJsonAsync("api/exercises/pool", request, ApiJson.Options, ct);

    /// <summary>
    /// Creates a brand-new custom exercise and adds it to the pool in one step. Returns the new
    /// pool item (its Id is needed to immediately log against it), or null if validation failed.
    /// </summary>
    public async Task<PoolItemDto?> AddCustomExerciseAsync(CreateCustomExerciseRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/exercises/custom", request, ApiJson.Options, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PoolItemDto>(ApiJson.Options, ct)
            : null;
    }

    public Task UpdatePoolItemAsync(int id, UpdatePoolItemRequest request, CancellationToken ct = default)
        => http.PutAsJsonAsync($"api/exercises/pool/{id}", request, ApiJson.Options, ct);

    public Task MovePoolItemAsync(int id, bool up, CancellationToken ct = default)
        => http.PostAsync($"api/exercises/pool/{id}/move?up={(up ? "true" : "false")}", content: null, ct);

    public Task DeactivatePoolItemAsync(int id, CancellationToken ct = default)
        => http.DeleteAsync($"api/exercises/pool/{id}", ct);
}
