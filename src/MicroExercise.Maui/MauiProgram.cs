using System.Net;
using MicroExercise.ApiClient;
using MicroExercise.Maui.Pages;
using MicroExercise.Maui.Services;
using MicroExercise.Maui.ViewModels;
using Microsoft.Extensions.Logging;

namespace MicroExercise.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var services = builder.Services;

        // --- Backend + HTTP. One cookie jar, shared by the HttpClient and the CookieStore, so the
        //     Identity cookie the server writes during login is the same one we persist/rehydrate. ---
        services.AddSingleton(BackendOptions.ForCurrentPlatform());
        services.AddSingleton<CookieContainer>();
        services.AddSingleton(sp =>
        {
            var backend = sp.GetRequiredService<BackendOptions>();
            var handler = new HttpClientHandler
            {
                CookieContainer = sp.GetRequiredService<CookieContainer>(),
                UseCookies = true,
                // /api returns a clean 401 (not a 302 to HTML), and we don't want to follow the
                // SSR login redirect into the SPA page — so never auto-redirect.
                AllowAutoRedirect = false,
            };
            return new HttpClient(handler) { BaseAddress = backend.BaseAddress };
        });

        // --- Auth + session ---
        services.AddSingleton<CookieStore>();
        services.AddSingleton<ISession, Session>();
        services.AddSingleton<AuthService>();

        // --- Typed REST clients (shared MicroExercise.ApiClient — identical to the web client) ---
        services.AddSingleton<PoolApi>();
        services.AddSingleton<LogApi>();
        services.AddSingleton<ReportApi>();
        services.AddSingleton<GoalApi>();

        // --- Shell, pages, view models ---
        services.AddTransient<AppShell>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<LoginPage>();
        services.AddSingleton<LogViewModel>();
        services.AddTransient<LogPage>();
        services.AddSingleton<HistoryViewModel>();
        services.AddTransient<HistoryPage>();
        services.AddSingleton<ReportsViewModel>();
        services.AddTransient<ReportsPage>();
        services.AddSingleton<GoalsViewModel>();
        services.AddTransient<GoalsPage>();
        services.AddSingleton<PoolViewModel>();
        services.AddTransient<PoolPage>();
        services.AddTransient<GoalsPage>();
        services.AddTransient<PoolPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
