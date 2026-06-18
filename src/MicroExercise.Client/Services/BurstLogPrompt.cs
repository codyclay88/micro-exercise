using MicroExercise.Core.Dtos;

namespace MicroExercise.Client.Services;

/// <summary>
/// App-wide mediator for logging a burst from anywhere. A caller (a Quick-Log card, a goal row,
/// the navbar's "＋ Log") raises <see cref="Request"/> to open the shared
/// <c>BurstLogDialog</c> — optionally pre-targeted to one exercise, otherwise letting the user pick.
/// After a burst is written, the dialog fires <see cref="Logged"/> so any screen can fold the result
/// into its own state (today's totals, goal progress) without a reload. Callers and listeners stay
/// decoupled: the card that opens the dialog doesn't know who's listening, and a screen reacting to
/// <see cref="Logged"/> doesn't care where the burst came from.
/// </summary>
/// <remarks>Registered scoped — effectively a singleton in the single-user WASM runtime.</remarks>
public sealed class BurstLogPrompt
{
    /// <summary>Raised to open the dialog. The argument pre-selects an exercise, or null to let the user pick.</summary>
    public event Action<PoolItemDto?>? Requested;

    /// <summary>Raised after a burst is successfully recorded anywhere in the app.</summary>
    public event Action<BurstLogged>? Logged;

    /// <summary>Open the shared burst-log dialog, optionally pre-targeted to <paramref name="item"/>.</summary>
    public void Request(PoolItemDto? item = null) => Requested?.Invoke(item);

    /// <summary>Invoked by the dialog after a successful POST so subscribers can update optimistically.</summary>
    public void NotifyLogged(BurstLogged result) => Logged?.Invoke(result);
}

/// <summary>
/// A burst that was just recorded — the payload of <see cref="BurstLogPrompt.Logged"/>.
/// <paramref name="CreatedPoolItem"/> is true when the dialog created a brand-new exercise for
/// this burst, so screens know to pull the refreshed pool rather than patch an existing card.
/// </summary>
public sealed record BurstLogged(
    int ExercisePoolId, int Quantity, DateTimeOffset Timestamp, bool CreatedPoolItem = false);
