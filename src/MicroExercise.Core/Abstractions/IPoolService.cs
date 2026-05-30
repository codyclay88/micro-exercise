using MicroExercise.Core.Dtos;

namespace MicroExercise.Core.Abstractions;

/// <summary>Manages a user's pool of Quick-Log exercises (spec §4.1 grid, §4.2 management).</summary>
public interface IPoolService
{
    /// <summary>The user's active pool entries, in dashboard display order.</summary>
    Task<IReadOnlyList<PoolItemDto>> GetActivePoolAsync(int userId, CancellationToken ct = default);

    /// <summary>The global exercise catalog available to add to a pool.</summary>
    Task<IReadOnlyList<ExerciseTypeDto>> GetExerciseTypesAsync(CancellationToken ct = default);

    /// <summary>Adds a new exercise to the user's pool. Throws if the exercise type is unknown.</summary>
    Task<PoolItemDto> AddPoolItemAsync(int userId, CreatePoolItemRequest request, CancellationToken ct = default);
}
