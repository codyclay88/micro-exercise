using MicroExercise.Core.Dtos;

namespace MicroExercise.Core.Abstractions;

/// <summary>Manages a user's pool of Quick-Log exercises (spec §4.1 grid, §4.2 management).</summary>
public interface IPoolService
{
    /// <summary>The user's active pool entries, in dashboard display order.</summary>
    Task<IReadOnlyList<PoolItemDto>> GetActivePoolAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Exercises the user can add to their pool: the global catalog plus the user's own
    /// custom exercises.
    /// </summary>
    Task<IReadOnlyList<ExerciseTypeDto>> GetExerciseTypesAsync(int userId, CancellationToken ct = default);

    /// <summary>Adds a new exercise to the user's pool. Throws if the exercise type is unknown.</summary>
    Task<PoolItemDto> AddPoolItemAsync(int userId, CreatePoolItemRequest request, CancellationToken ct = default);

    /// <summary>
    /// Creates a brand-new custom exercise owned by the user and adds it to their pool.
    /// The exercise then appears in the user's catalog for reuse.
    /// </summary>
    Task<PoolItemDto> AddCustomExerciseAsync(int userId, CreateCustomExerciseRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates a pool entry's custom name and target quantity. Returns the updated entry,
    /// or <c>null</c> if it does not exist or is not owned by the user.
    /// </summary>
    Task<PoolItemDto?> UpdatePoolItemAsync(
        int userId, int poolId, UpdatePoolItemRequest request, CancellationToken ct = default);

    /// <summary>
    /// Moves an active pool entry up or down in dashboard order by swapping sort positions
    /// with its neighbour. Returns <c>false</c> if there is no neighbour in that direction,
    /// or the entry is missing / not owned by the user.
    /// </summary>
    Task<bool> MovePoolItemAsync(int userId, int poolId, bool up, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a pool entry (clears <c>IsActive</c>), removing it from the grid while
    /// preserving its historical logs. Returns <c>false</c> if missing / not owned.
    /// </summary>
    Task<bool> DeactivatePoolItemAsync(int userId, int poolId, CancellationToken ct = default);
}
