using System.Text.Json.Serialization;
using MicroExercise.Core.Abstractions;
using MicroExercise.Infrastructure;
using MicroExercise.Infrastructure.Data;
using MicroExercise.Infrastructure.Identity;
using MicroExercise.Web.Authentication;
using MicroExercise.Web.Components;
using MicroExercise.Web.Endpoints;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Managed container hosts (Railway, Render, Fly, …) inject the port to listen on via $PORT.
// Honor it when present; otherwise fall back to ASPNETCORE_HTTP_PORTS (8080 in the Dockerfile)
// for local runs and the DigitalOcean Compose stack.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Razor Components are static-SSR only now (login/register/error). The app UI is a separate
// Blazor WebAssembly SPA (MicroExercise.Client) served as static files — no SignalR circuit.
builder.Services.AddRazorComponents();

var connectionString = builder.Configuration.GetConnectionString("AppDb")
    ?? throw new InvalidOperationException("Connection string 'AppDb' was not found.");
builder.Services.AddInfrastructure(connectionString);

// Behind the production reverse proxy (Caddy), TLS terminates at the proxy and the app is
// reached over plain HTTP. Honor X-Forwarded-Proto/For so the request is seen as HTTPS
// (Identity then marks its auth cookie Secure and UseHttpsRedirection won't loop). The proxy
// address is dynamic inside the Compose network, so trust the forwarding chain there.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Persist Data Protection keys outside the container so auth cookies and antiforgery tokens
// survive redeploys (the default in-container key ring is ephemeral and would log everyone out
// on every deploy). The path is a mounted volume in compose.yaml. Dev keeps the default ring.
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("/keys"));
}

// --- ASP.NET Core Identity (cookie auth, integer keys) ---
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
    })
    .AddIdentityCookies();

// Authorization services (the /api group uses RequireAuthorization). Previously pulled in
// transitively by the interactive-server components stack, which is now gone.
builder.Services.AddAuthorization();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false; // no email service in the MVP
        options.User.RequireUniqueEmail = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 6;
    })
    .AddRoles<IdentityRole<int>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Point the Identity application cookie at our themed pages and give it a long,
// sliding lifetime for seamless desktop sessions (spec §2).
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;

    // The WASM client calls /api over fetch; an unauthenticated request must get a 401/403
    // (so the client knows to send the user to /login) rather than a 302 redirect to the
    // HTML login page, which fetch would silently follow. Browser navigations still redirect.
    options.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else
            ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        else
            ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// Serialize enums (e.g. TrackingType) as strings in the JSON API contract.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// Apply migrations on startup. Global exercise catalog is seeded via HasData; user data
// is created on registration.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Apply proxy-forwarded scheme/host before anything that inspects the request (auth,
// redirects). No-op in dev where there's no proxy and no forwarded headers are sent.
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseHttpsRedirection();

// Serve the WASM client's framework files (_framework/*) and the static assets in wwwroot
// BEFORE routing, so they short-circuit the index.html fallback. (Classic UseStaticFiles, not
// MapStaticAssets — the latter's fingerprinted endpoints can't serve the Blazor _framework files.)
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<App>();   // static SSR: login / register / error
app.MapApiEndpoints();

// Liveness probe for the host's health checks (Railway healthcheckPath, etc.). Anonymous,
// no DB — just confirms the process is serving.
app.MapGet("/healthz", () => Results.Ok("ok"));

// Sign out (posted from the nav). The matching login/register live in Components/Account.
// Antiforgery disabled so the WASM client's plain form POST works (logout just clears the cookie).
app.MapPost("/account/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.LocalRedirect("/login");
}).DisableAntiforgery();

// Everything not matched above (the SPA's client-side routes) returns the WASM host page.
app.MapFallbackToFile("index.html");

app.Run();
