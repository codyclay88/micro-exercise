using Microsoft.Maui.Devices;

namespace MicroExercise.Maui.Services;

/// <summary>
/// Resolves the backend base address. Production points at the deployed host; dev points at the
/// local server — with the platform-specific loopback, since the Android emulator can't reach
/// the host's "localhost" (it tunnels via 10.0.2.2). See docs/MAUI-Mobile-App-Design.md §7.
/// </summary>
public sealed class BackendOptions
{
    public required Uri BaseAddress { get; init; }

    public static BackendOptions ForCurrentPlatform()
    {
#if DEBUG
        var url = DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5077/"     // Android emulator -> host loopback
            : "http://localhost:5077/";   // Windows desktop / iOS simulator
#else
        var url = "https://exercise.codyclay.com/";
#endif
        return new BackendOptions { BaseAddress = new Uri(url) };
    }
}
