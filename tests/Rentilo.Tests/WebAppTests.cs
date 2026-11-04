using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.Services;

namespace Rentilo.Tests;

/// <summary>Boots the real app with SQLite in-memory instead of PostgreSQL, seeded with demo data.</summary>
public sealed class RentiloFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused");
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Database:SeedDemoData", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        SeedData.SeedDemoDataAsync(scope.ServiceProvider).GetAwaiter().GetResult();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

public class WebAppTests(RentiloFactory factory) : IClassFixture<RentiloFactory>
{
    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("/")]
    [InlineData("/Items")]
    [InlineData("/Items?q=drill&category=Tools")]
    [InlineData("/Items/Details/1")]
    [InlineData("/HowItWorks")]
    [InlineData("/Privacy")]
    [InlineData("/Terms")]
    [InlineData("/Identity/Account/Login")]
    [InlineData("/Identity/Account/Register")]
    [InlineData("/healthz")]
    public async Task Public_pages_load(string url)
    {
        var response = await Client().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/Dashboard")]
    [InlineData("/Dashboard/Requests")]
    [InlineData("/Bookings")]
    [InlineData("/Items/Create")]
    [InlineData("/Items/Edit/1")]
    [InlineData("/Identity/Account/Manage")]
    public async Task Private_pages_redirect_to_login(string url)
    {
        var response = await Client().GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("/does-not-exist")]
    [InlineData("/Items/Details/99999")]
    public async Task Unknown_pages_return_friendly_404(string url)
    {
        var response = await Client().GetAsync(url);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Page not found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Search_finds_matching_items()
    {
        var html = await Client().GetStringAsync("/Items?q=tent");
        Assert.Contains("4-person camping tent", html);
        Assert.DoesNotContain("Acoustic guitar", html);
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await Client().GetAsync("/");
        Assert.Contains("default-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Booking_post_without_antiforgery_token_is_rejected()
    {
        var response = await Client().PostAsync("/Items/Details/1?handler=Book", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.StartDate"] = "2030-01-01",
            ["Input.EndDate"] = "2030-01-02",
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Account_deletion_removes_user_and_their_data_but_blocks_active_rentals()
    {
        using var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var bookings = sp.GetRequiredService<BookingService>();
        var deletion = sp.GetRequiredService<AccountDeletionService>();

        var user = new ApplicationUser { UserName = "del@test", Email = "del@test", FullName = "Del Eted" };
        Assert.True((await users.CreateAsync(user, "Password1")).Succeeded);
        var item = new Item { Title = "Ladder", Description = "Tall ladder.", PricePerDay = 50, Location = "Lund", OwnerId = user.Id };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var renter = await users.FindByEmailAsync("anna@rentilo.dev");
        var booking = (await bookings.RequestAsync(item.Id, renter!.Id, bookings.Today.AddDays(1), bookings.Today.AddDays(2), null)).Value!;
        Assert.True((await bookings.AcceptAsync(booking.Id, user.Id)).Succeeded);

        var blocked = await deletion.DeleteAsync(user);
        Assert.False(blocked.Succeeded);

        Assert.True((await bookings.CancelAsync(booking.Id, renter.Id)).Succeeded);
        var deleted = await deletion.DeleteAsync(user);
        Assert.True(deleted.Succeeded, deleted.Error);

        db.ChangeTracker.Clear();
        Assert.Null(await users.FindByIdAsync(user.Id));
        Assert.False(await db.Items.AnyAsync(i => i.Id == item.Id));
        Assert.False(await db.Bookings.AnyAsync(b => b.Id == booking.Id));
    }
}
