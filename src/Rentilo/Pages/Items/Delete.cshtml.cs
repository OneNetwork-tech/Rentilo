using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.Services;

namespace Rentilo.Pages.Items;

/// <summary>
/// Items with booking history can't be hard-deleted (renters keep their records); they're paused instead.
/// Items without any bookings are deleted completely, including their photo.
/// </summary>
public class DeleteModel(ApplicationDbContext db, IImageStorage images, UserManager<ApplicationUser> users) : PageModel
{
    public Item Item { get; private set; } = null!;
    public bool HasBookings { get; private set; }
    public bool HasActiveBookings { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id) => await LoadAsync(id) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (HasActiveBookings)
        {
            TempData["Error"] = "This item has pending or upcoming bookings. Decline or finish them first.";
            return RedirectToPage("/Dashboard/Requests");
        }

        var item = await db.Items.FirstAsync(i => i.Id == id);
        if (HasBookings)
        {
            item.IsAvailable = false;
            await db.SaveChangesAsync();
            TempData["Success"] = "The listing has booking history, so it was paused and hidden instead of deleted.";
        }
        else
        {
            db.Items.Remove(item);
            await db.SaveChangesAsync();
            images.Delete(item.ImagePath);
            TempData["Success"] = "Listing deleted.";
        }
        return RedirectToPage("/Dashboard/Listings");
    }

    private async Task<bool> LoadAsync(int id)
    {
        var userId = users.GetUserId(User);
        var item = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id && i.OwnerId == userId);
        if (item is null) return false;
        Item = item;
        var today = DateOnly.FromDateTime(DateTime.Now);
        HasBookings = await db.Bookings.AnyAsync(b => b.ItemId == id);
        HasActiveBookings = await db.Bookings.AnyAsync(b => b.ItemId == id &&
            (b.Status == BookingStatus.Pending || (b.Status == BookingStatus.Accepted && b.EndDate >= today)));
        return true;
    }
}
