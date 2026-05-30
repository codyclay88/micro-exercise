using System.Security.Claims;
using System.Text.Json.Serialization;
using MicroExercise.Core;
using MicroExercise.Core.Abstractions;
using MicroExercise.Infrastructure;
using MicroExercise.Infrastructure.Data;
using MicroExercise.Web.Authentication;
using MicroExercise.Web.Components;
using MicroExercise.Web.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("AppDb")
    ?? throw new InvalidOperationException("Connection string 'AppDb' was not found.");
builder.Services.AddInfrastructure(connectionString);

// Cookie-based auth (HttpOnly/SameSite) — Identity-ready; see spec §2.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// Serialize enums (e.g. TrackingType) as strings in the JSON API contract.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// Apply migrations and seed sample data on startup (MVP convenience).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.SeedAsync(db);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();

// MVP auto-login: establish a cookie for the seeded demo user on first request,
// and treat the current request as that user. Replace with real login later.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated != true)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, AppDefaults.DemoUserId.ToString()),
            new Claim(ClaimTypes.Name, AppDefaults.DemoUserDisplayName),
            new Claim(ClaimTypes.Email, AppDefaults.DemoUserEmail)
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);
        context.User = principal;
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }

    await next();
});

app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapApiEndpoints();

app.Run();
