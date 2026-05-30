namespace MicroExercise.Core.Abstractions;

/// <summary>
/// Resolves the identity of the caller. For the MVP this yields the seeded demo user;
/// a real identity provider can replace the implementation without touching services.
/// </summary>
public interface ICurrentUser
{
    int UserId { get; }
}
