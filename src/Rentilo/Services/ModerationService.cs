using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;

namespace Rentilo.Services;

public static class Roles
{
    public const string Admin = "Admin";
}

/// <summary>Reports, listing moderation, user locking and review removal.</summary>
public class ModerationService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole> roles,
    IConfiguration config,
    TimeProvider clock,
    ILogger<ModerationService> logger)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Emails listed in <c>Admin:Emails</c> (comma or semicolon separated).</summary>
    public IReadOnlySet<string> AdminEmails =>
        (config["Admin:Emails"] ?? "")
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ensures the Admin role exists and every configured admin email that has an account holds it.</summary>
    public async Task SyncAdminsAsync()
    {
        if (!await roles.RoleExistsAsync(Roles.Admin))
            await roles.CreateAsync(new IdentityRole(Roles.Admin));
        foreach (var email in AdminEmails)
        {
            var user = await users.FindByEmailAsync(email);
            if (user is not null) await GrantAdminIfConfiguredAsync(user);
        }
    }

    public async Task GrantAdminIfConfiguredAsync(ApplicationUser user)
    {
        if (user.Email is null || !AdminEmails.Contains(user.Email)) return;
        if (!await roles.RoleExistsAsync(Roles.Admin)) await roles.CreateAsync(new IdentityRole(Roles.Admin));
        if (!await users.IsInRoleAsync(user, Roles.Admin))
        {
            await users.AddToRoleAsync(user, Roles.Admin);
            logger.LogInformation("Granted admin role to {Email}", user.Email);
        }
    }

    // ---- Reports ------------------------------------------------------------------------------

    public async Task<ServiceResult> ReportAsync(int itemId, string reporterId, ReportReason reason, string? details)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId);
        if (item is null) return ServiceResult.Fail("This item no longer exists.");
        if (item.OwnerId == reporterId) return ServiceResult.Fail("You can't report your own listing.");
        if (await db.Reports.AnyAsync(r => r.ItemId == itemId && r.ReporterId == reporterId && r.ResolvedAt == null))
            return ServiceResult.Fail("You've already reported this listing. A moderator will look at it.");

        db.Reports.Add(new Report
        {
            ItemId = itemId,
            ReporterId = reporterId,
            Reason = reason,
            Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
            CreatedAt = Now,
        });
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>Closes all open reports for an item with the given resolution text.</summary>
    public async Task ResolveReportsAsync(int itemId, string adminId, string resolution)
    {
        var open = await db.Reports.Where(r => r.ItemId == itemId && r.ResolvedAt == null).ToListAsync();
        foreach (var r in open)
        {
            r.ResolvedAt = Now;
            r.ResolvedById = adminId;
            r.Resolution = resolution;
        }
        await db.SaveChangesAsync();
    }

    // ---- Listings -----------------------------------------------------------------------------

    public async Task<ServiceResult> HideItemAsync(int itemId, string adminId, string? note)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId);
        if (item is null) return ServiceResult.Fail("Item not found.");
        item.IsHiddenByAdmin = true;
        item.ModerationNote = string.IsNullOrWhiteSpace(note) ? "This listing breaks the Rentilo terms." : note.Trim();

        // Pending requests for a hidden item can't go ahead.
        var pending = await db.Bookings.Where(b => b.ItemId == itemId && b.Status == BookingStatus.Pending).ToListAsync();
        foreach (var b in pending)
        {
            b.Status = BookingStatus.Declined;
            b.UpdatedAt = Now;
        }
        await db.SaveChangesAsync();
        await ResolveReportsAsync(itemId, adminId, "Listing hidden");
        logger.LogInformation("Admin {AdminId} hid item {ItemId}", adminId, itemId);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UnhideItemAsync(int itemId)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId);
        if (item is null) return ServiceResult.Fail("Item not found.");
        item.IsHiddenByAdmin = false;
        item.ModerationNote = null;
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    // ---- Users --------------------------------------------------------------------------------

    public async Task<ServiceResult> LockUserAsync(string userId, string adminId)
    {
        if (userId == adminId) return ServiceResult.Fail("You can't lock your own account.");
        var user = await users.FindByIdAsync(userId);
        if (user is null) return ServiceResult.Fail("User not found.");
        if (await users.IsInRoleAsync(user, Roles.Admin)) return ServiceResult.Fail("Admins can't be locked. Remove them from Admin:Emails first.");

        await users.SetLockoutEnabledAsync(user, true);
        await users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        // Invalidates existing login cookies (checked every few minutes, see Program.cs).
        await users.UpdateSecurityStampAsync(user);
        logger.LogInformation("Admin {AdminId} locked user {UserId}", adminId, userId);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UnlockUserAsync(string userId)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null) return ServiceResult.Fail("User not found.");
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return ServiceResult.Ok();
    }

    public async Task<bool> IsLockedAsync(ApplicationUser user) =>
        user.LockoutEnd is { } end && end > clock.GetUtcNow();

    // ---- Reviews ------------------------------------------------------------------------------

    public async Task<ServiceResult> DeleteReviewAsync(int reviewId, string adminId)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId);
        if (review is null) return ServiceResult.Fail("Review not found.");
        db.Reviews.Remove(review);
        await db.SaveChangesAsync();
        logger.LogInformation("Admin {AdminId} deleted review {ReviewId}", adminId, reviewId);
        return ServiceResult.Ok();
    }
}
