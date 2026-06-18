using MicroExercise.Core.Dtos;

namespace MicroExercise.Maui.Services;

/// <summary>Holds the signed-in user for the app's lifetime; raises <see cref="Changed"/> on login/logout.</summary>
public interface ISession
{
    CurrentUserDto? CurrentUser { get; }
    bool IsAuthenticated { get; }
    event EventHandler? Changed;
    void Set(CurrentUserDto? user);
}

public sealed class Session : ISession
{
    public CurrentUserDto? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;
    public event EventHandler? Changed;

    public void Set(CurrentUserDto? user)
    {
        CurrentUser = user;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
