using ClubManager.Components;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ClubManager.Data;
using ClubManager.Services;
using MercenariesAndBeasts.Infrastructure;
using MercenariesAndBeasts.Infrastructure.Auth;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using Radzen;
using Serilog;
using Serilog.Exceptions;
using Serilog.Sinks.PostgreSQL.ColumnWriters;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Logs"));
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .Enrich.FromLogContext()
    .Enrich.WithExceptionDetails()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        shared: true,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
    .WriteTo.PostgreSQL(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection") ?? "",
        tableName: "Logs",
        columnOptions: (IDictionary<string, ColumnWriterBase>?)null,
        needAutoCreateTable: true,
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning)
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRazorPages();

// ── UI ────────────────────────────────────────────────────────────────────────
builder.Services.AddMudServices();
builder.Services.AddRadzenComponents();

// ── Notification config ───────────────────────────────────────────────────────
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<NtfySettings>(builder.Configuration.GetSection("Ntfy"));
builder.Services.AddHttpClient<ClubNotificationService>();
builder.Services.AddScoped<ClubNotificationService>();

// ── Domain services ───────────────────────────────────────────────────────────
builder.Services.AddScoped<ClubService>();
builder.Services.AddScoped<MessageService>();
builder.Services.AddScoped<CarReservationService>();

// ── DB ────────────────────────────────────────────────────────────────────────
var cs = builder.Configuration.GetConnectionString("DefaultConnection");
var dsb = new NpgsqlDataSourceBuilder(cs);
dsb.EnableDynamicJson();
var dataSource = dsb.Build();

builder.Services.AddMabDbContext<AppDbContextClubManager>(dataSource, configure: opt =>
    opt.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// ── Identity ──────────────────────────────────────────────────────────────────
builder.Services.AddMabAuth<AppDbContextClubManager>(builder.Configuration);
builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, NoOpEmailSender>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks();

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    Log.Fatal(e.ExceptionObject as Exception, "UNHANDLED AppDomain exception");
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    Log.Fatal(e.Exception, "UNOBSERVED task exception");
    e.SetObserved();
};

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.MapHealthChecks("/health");
app.MapStaticAssets();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorPages();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(MercenariesAndBeasts.Infrastructure.Components.Account.Login).Assembly);

// ── Google OAuth ──────────────────────────────────────────────────────────────
app.MapPost("/Identity/Account/ExternalLogin", async (HttpContext http, SignInManager<AppUser> signInManager) =>
{
    var provider  = http.Request.Form["provider"].ToString();
    var returnUrl = http.Request.Form["returnUrl"].ToString() ?? "/";
    var callback  = $"/Identity/Account/ExternalLogin/Callback?returnUrl={Uri.EscapeDataString(returnUrl)}";
    var props     = signInManager.ConfigureExternalAuthenticationProperties(provider, callback);
    return Results.Challenge(props, new[] { provider });
}).DisableAntiforgery();

app.MapGet("/Identity/Account/ExternalLogin/Callback", async (
    HttpContext http,
    string? returnUrl,
    SignInManager<AppUser> signInManager,
    UserManager<AppUser> userManager,
    IWebHostEnvironment env,
    IConfiguration config) =>
{
    returnUrl ??= "/";
    var info = await signInManager.GetExternalLoginInfoAsync();
    if (info is null) return Results.Redirect("/login?error=external");

    var signIn = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true);
    if (signIn.Succeeded)
    {
        var u = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (u is not null)
        {
            var denied = await AccessGate.CheckAsync(u, signInManager, env, config);
            if (denied is not null) return Results.Redirect(denied);
        }
        return Results.Redirect(returnUrl);
    }

    var email = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.Email) ?? "";
    if (string.IsNullOrWhiteSpace(email)) return Results.Redirect("/login?error=noemail");

    var user = new AppUser { UserName = email, Email = email };
    var created = await userManager.CreateAsync(user);
    if (created.Succeeded)
    {
        await userManager.AddLoginAsync(user, info);
        await signInManager.SignInAsync(user, isPersistent: true);
        return Results.Redirect(returnUrl);
    }

    var existing = await userManager.FindByEmailAsync(email);
    if (existing is not null)
    {
        await userManager.AddLoginAsync(existing, info);
        await signInManager.SignInAsync(existing, isPersistent: true);
        return Results.Redirect(returnUrl);
    }

    return Results.Redirect("/login?error=external");
});

// ── Migrate + Seed ────────────────────────────────────────────────────────────
try
{
    using var scope = app.Services.CreateScope();
    var db          = scope.ServiceProvider.GetRequiredService<AppDbContextClubManager>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await db.Database.MigrateAsync();
    await SeedAsync(userManager, roleManager);
}
catch (Exception ex) { Log.Warning(ex, "DB migration/seed skipped"); }

app.Lifetime.ApplicationStopping.Register(() => Log.Warning("Application stopping — flushing logs..."));

try { app.Run(); }
catch (Exception ex) { Log.Fatal(ex, "Host terminated unexpectedly"); }
finally { Log.CloseAndFlush(); }

// ── Helpers ───────────────────────────────────────────────────────────────────
static async Task SeedAsync(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
{
    foreach (var r in new[] { "Admin", "Moderator", "LoginUser", "ClubManager" })
        if (!await roleManager.RoleExistsAsync(r))
            await roleManager.CreateAsync(new IdentityRole(r));

    var user = await userManager.FindByEmailAsync("olsanskyvitek@gmail.com");
    if (user is null)
    {
        user = new AppUser
        {
            UserName = "vitek",
            Email = "olsanskyvitek@gmail.com",
            EmailConfirmed = true,
            IsAdmin = true,
            IsWhitelisted = true,
            MustChangePassword = true
        };
        var result = await userManager.CreateAsync(user, "Vitek575");
        if (!result.Succeeded)
            throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    foreach (var role in new[] { "Admin", "Moderator", "LoginUser", "ClubManager" })
        if (!await userManager.IsInRoleAsync(user, role))
            await userManager.AddToRoleAsync(user, role);
}

file sealed class NoOpEmailSender : Microsoft.AspNetCore.Identity.UI.Services.IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage) => Task.CompletedTask;
}

public partial class Program { }
