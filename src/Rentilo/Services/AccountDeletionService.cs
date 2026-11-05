using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;

namespace Rentilo.Services;

/// <summary>
/// Deletes a user and everything they own (GDPR "right to erasure"). Foreign keys are Restrict on purpose,
/// so this service deletes dependants explicitly and in the right order.
/// Deletion is refused while an accepted rental (as renter or owner) is ongoing or upcoming, because the
/// other person depends on it; pending requests are closed automatically.
/// </summary>
public class AccountDeletionService(ApplicationDbContext db, UserManager<ApplicationUser> users, IImageStorage images, TimeProvider clock)
{
    public async Task<ServiceResult> DeleteAsync(ApplicationUser user)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var hasCommitments = await db.Bookings.AnyAsync(b =>
            (b.RenterId == user.Id || b.Item.OwnerId == user.Id) &&
            b.Status == BookingStatus.Accepted && b.EndDate >= today);
        if (hasCommitments)
            return ServiceResult.Fail("You have accepted rentals that haven't finished yet. Cancel or complete them before deleting your account.");

        await using var tx = await db.Database.BeginTransactionAsync();

        var itemIds = await db.Items.Where(i => i.OwnerId == user.Id).Select(i => i.Id).ToListAsync();
        var imagePaths = await db.Items.Where(i => i.OwnerId == user.Id && i.ImagePath != null).Select(i => i.ImagePath!).ToListAsync();

        db.Reviews.RemoveRange(await db.Reviews.Where(r => r.AuthorId == user.Id || itemIds.Contains(r.ItemId)).ToListAsync());
        await db.SaveChangesAsync();
        db.Bookings.RemoveRange(await db.Bookings.Where(b => b.RenterId == user.Id || itemIds.Contains(b.ItemId)).ToListAsync());
        await db.SaveChangesAsync();
        db.Items.RemoveRange(await db.Items.Where(i => i.OwnerId == user.Id).ToListAsync());
        await db.SaveChangesAsync();

        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
            return ServiceResult.Fail("Unexpected error while deleting your account.");

        await tx.CommitAsync();
        foreach (var path in imagePaths) images.Delete(path);
        return ServiceResult.Ok();
    }
}
