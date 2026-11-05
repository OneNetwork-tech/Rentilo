using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.Services;
using Rentilo.ViewModels;

namespace Rentilo.Tests;

public class ModerationTests(RentiloFactory factory) : IClassFixture<RentiloFactory>
{
    private async Task<(IServiceScope Scope, ModerationService Moderation, ApplicationDbContext Db, UserManager<ApplicationUser> Users)> ScopeAsync()
    {
        var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var moderation = sp.GetRequiredService<ModerationService>();
        await moderation.SyncAdminsAsync();
        return (scope, moderation, sp.GetRequiredService<ApplicationDbContext>(), sp.GetRequiredService<UserManager<ApplicationUser>>());
    }

    private static async Task<ApplicationUser> NewUserAsync(UserManager<ApplicationUser> users, string prefix)
    {
        var email = $"{prefix}{Guid.NewGuid():N}@test";
        var user = new ApplicationUser { UserName = email, Email = email, FullName = prefix };
        Assert.True((await users.CreateAsync(user, "Password1")).Succeeded);
        return user;
    }

    [Fact]
    public async Task Configured_admin_email_gets_the_admin_role()
    {
        var (scope, _, _, users) = await ScopeAsync();
        using (scope)
        {
            var anna = await users.FindByEmailAsync("anna@rentilo.dev");
            Assert.True(await users.IsInRoleAsync(anna!, Roles.Admin));
            var erik = await users.FindByEmailAsync("erik@rentilo.dev");
            Assert.False(await users.IsInRoleAsync(erik!, Roles.Admin));
        }
    }

    [Fact]
    public async Task Reports_reject_own_item_and_duplicates()
    {
        var (scope, moderation, db, users) = await ScopeAsync();
        using (scope)
        {
            var reporter = await NewUserAsync(users, "reporter");
            var item = await db.Items.FirstAsync();

            Assert.False((await moderation.ReportAsync(item.Id, item.OwnerId, ReportReason.Scam, null)).Succeeded);
            Assert.True((await moderation.ReportAsync(item.Id, reporter.Id, ReportReason.Scam, "  fake  ")).Succeeded);
            Assert.False((await moderation.ReportAsync(item.Id, reporter.Id, ReportReason.Other, null)).Succeeded);
            Assert.Equal("fake", (await db.Reports.SingleAsync(r => r.ReporterId == reporter.Id)).Details);
        }
    }

    [Fact]
    public async Task Hiding_an_item_removes_it_from_listings_declines_pending_and_closes_reports()
    {
        var (scope, moderation, db, users) = await ScopeAsync();
        using (scope)
        {
            var owner = await NewUserAsync(users, "owner");
            var renter = await NewUserAsync(users, "renter");
            var admin = await users.FindByEmailAsync("anna@rentilo.dev");
            var item = new Item { Title = "Suspicious thing", Description = "Very suspicious.", PricePerDay = 10, Location = "Lund", OwnerId = owner.Id };
            db.Items.Add(item);
            await db.SaveChangesAsync();
            var bookings = scope.ServiceProvider.GetRequiredService<BookingService>();
            var pending = (await bookings.RequestAsync(item.Id, renter.Id, bookings.Today.AddDays(1), bookings.Today.AddDays(2), null)).Value!;
            await moderation.ReportAsync(item.Id, renter.Id, ReportReason.Prohibited, null);

            Assert.True((await moderation.HideItemAsync(item.Id, admin!.Id, "Not allowed")).Succeeded);

            db.ChangeTracker.Clear();
            Assert.False(await db.Items.Listed().AnyAsync(i => i.Id == item.Id));
            Assert.Equal(BookingStatus.Declined, (await db.Bookings.FindAsync(pending.Id))!.Status);
            Assert.False(await db.Reports.AnyAsync(r => r.ItemId == item.Id && r.ResolvedAt == null));
            Assert.Equal("Not allowed", (await db.Items.FindAsync(item.Id))!.ModerationNote);

            Assert.True((await moderation.UnhideItemAsync(item.Id)).Succeeded);
            Assert.True(await db.Items.Listed().AnyAsync(i => i.Id == item.Id));
        }
    }

    [Fact]
    public async Task Locking_users_blocks_sign_in_but_never_admins_or_yourself()
    {
        var (scope, moderation, _, users) = await ScopeAsync();
        using (scope)
        {
            var admin = await users.FindByEmailAsync("anna@rentilo.dev");
            var user = await NewUserAsync(users, "bad");

            Assert.False((await moderation.LockUserAsync(admin!.Id, admin.Id)).Succeeded);
            Assert.True((await moderation.LockUserAsync(user.Id, admin.Id)).Succeeded);
            Assert.True(await users.IsLockedOutAsync((await users.FindByIdAsync(user.Id))!));

            var other = await NewUserAsync(users, "other");
            Assert.False((await moderation.LockUserAsync(admin.Id, other.Id)).Succeeded, "admins can't be locked");

            Assert.True((await moderation.UnlockUserAsync(user.Id)).Succeeded);
            Assert.False(await users.IsLockedOutAsync((await users.FindByIdAsync(user.Id))!));
        }
    }

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Admin/Users")]
    [InlineData("/Bookings/Messages")]
    [InlineData("/Bookings/Conversation/1")]
    [InlineData("/Items/Report/1")]
    public async Task New_private_pages_require_login(string url)
    {
        var response = await factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }).GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location!.ToString());
    }
}
