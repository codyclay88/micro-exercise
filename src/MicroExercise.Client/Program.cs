using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MicroExercise.Client;
using MicroExercise.Client.Authentication;
using MicroExercise.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Same-origin HttpClient — the browser attaches the auth cookie automatically, so API calls
// are authenticated without any token plumbing.
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CookieAuthStateProvider>();

// Typed REST clients (the server derives the user from the cookie, so no userId is passed).
builder.Services.AddScoped<PoolApi>();
builder.Services.AddScoped<LogApi>();
builder.Services.AddScoped<ReportApi>();

await builder.Build().RunAsync();
