using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.Services;

namespace Rentilo.Pages.Bookings;

/// <summary>The current user's rentals (bookings where they are the renter).</summary>
public class IndexModel(ApplicationDbContext db, BookingService bookings, UserManager<ApplicationUser> users) : PageModel
{
    public List<Booking> Active { get; private set; } = [];
    public List<Booking> Past { get; private set; } = [];
    public BookingService Rules => bookings;
    public DateOnly Today => bookings.Today;

    public async Task OnGetAsync()
    {
        var userId = users.GetUserId(User);
        var all = await db.Bookings.AsNoTracking()
            .Include(b => b.Item).ThenInclude(i => i.Owner)
            .Include(b => b.Review)
            .Where(b => b.RenterId == userId)
            .OrderByDescending(b => b.StartDate)
            .ToListAsync();
        var today = Today;
        Active = all.Where(b => b.EndDate >= today && b.Status is BookingStatus.Pending or BookingStatus.Accepted)
            .OrderBy(b => b.StartDate).ToList();
        Past = all.Except(Active).ToList();
    }

    public async Task<IActionResult> OnPostCancelAsync(int bookingId)
    {
        var result = await bookings.CancelAsync(bookingId, users.GetUserId(User)!);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Booking cancelled." : result.Error;
        return RedirectToPage();
    }
}
