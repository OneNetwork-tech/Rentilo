using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.ViewModels;

namespace Rentilo.Pages.Users;

/// <summary>Public profile. Shows only public info (never email or phone).</summary>
public class ProfileModel(ApplicationDbContext db) : PageModel
{
    public ApplicationUser Profile { get; private set; } = null!;
    public List<ItemCard> Items { get; private set; } = [];
    public double? Rating { get; private set; }
    public int ReviewCount { get; private set; }
    public int CompletedRentals { get; private set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        Profile = user;
        Items = await db.Items.AsNoTracking().Listed().Where(i => i.OwnerId == id)
            .OrderByDescending(i => i.DateListed).ToCards().ToListAsync();
        var reviews = db.Reviews.Where(r => r.Item.OwnerId == id);
        Rating = await reviews.AverageAsync(r => (double?)r.Rating);
        ReviewCount = await reviews.CountAsync();
        CompletedRentals = await db.Bookings.CountAsync(b => b.Item.OwnerId == id && b.Status == BookingStatus.Completed);
        return Page();
    }
}
