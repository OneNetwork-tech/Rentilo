using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Infrastructure;
using Rentilo.Models;
using Rentilo.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Database -------------------------------------------------------------------------------
// The connection string is NOT stored in appsettings.json. Provide it via user secrets
// (dotnet user-secrets set "ConnectionStrings:DefaultConnection" "...") or the
// ConnectionStrings__DefaultConnection environment variable. See README.md.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found. Set it with user secrets or the " +
        "ConnectionStrings__DefaultConnection environment variable (see README.md).");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// --- Identity -------------------------------------------------------------------------------
builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = builder.Configuration.GetValue("Identity:RequireConfirmedAccount", false);
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Re-check login cookies against the database every few minutes, so locked users are signed out quickly.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(5));
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", p => p.RequireRole(Roles.Admin));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
});

// Persist the keys that protect login cookies and antiforgery tokens. Without this, every container
// rebuild generates new keys and signs everyone out. Docker sets DataProtection__KeysPath to a volume.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrEmpty(keysPath))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("Rentilo")
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Email:Smtp"));
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());
builder.Services.AddHostedService<EmailBackgroundSender>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<INotifier, EmailNotifier>();

// --- Application services -------------------------------------------------------------------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<AccountDeletionService>();
builder.Services.AddScoped<MessageService>();
builder.Services.AddScoped<ModerationService>();
builder.Services.AddSingleton<IImageStorage, LocalImageStorage>();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Bookings");
    options.Conventions.AuthorizeFolder("/Dashboard");
    options.Conventions.AuthorizePage("/Items/Create");
    options.Conventions.AuthorizePage("/Items/Edit");
    options.Conventions.AuthorizePage("/Items/Delete");
    options.Conventions.AuthorizePage("/Items/Report");
    options.Conventions.AuthorizeFolder("/Admin", "Admin");
});

builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Behind a reverse proxy (Docker, Azure App Service, nginx...) trust X-Forwarded-* headers.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// --- Database migration & seeding ------------------------------------------------------------
if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
}
if (app.Configuration.GetValue("Database:SeedDemoData", false))
{
    using var scope = app.Services.CreateScope();
    await SeedData.SeedDemoDataAsync(scope.ServiceProvider);
}
if (app.Configuration.GetValue("Database:MigrateOnStartup", false) || app.Configuration.GetValue("Database:SeedDemoData", false))
{
    // Only when we know the schema exists (tests and manual setups create it themselves).
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ModerationService>().SyncAdminsAsync();
}

// --- HTTP pipeline ---------------------------------------------------------------------------
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/StatusCode", "?code={0}");
app.UseSecurityHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/healthz");
app.MapRazorPages();

app.Run();

/// <summary>Exposed so integration tests can use WebApplicationFactory&lt;Program&gt;.</summary>
public partial class Program;
