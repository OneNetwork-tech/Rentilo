using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.Services;

namespace Rentilo.Pages.Bookings;

public class ReviewModel(ApplicationDbContext db, BookingService bookings, UserManager<ApplicationUser> users) : PageModel
{
    public Booking Booking { get; private set; } = null!;

    [BindProperty, Range(1, 5, ErrorMessage = "Please choose a rating.")]
    public int Rating { get; set; }

    [BindProperty, StringLength(1000), Display(Name = "Your review (optional)")]
    public string? Comment { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!bookings.CanReview(Booking))
        {
            TempData["Error"] = "This rental can't be reviewed (yet).";
            return RedirectToPage("Index");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!ModelState.IsValid) return Page();

        var result = await bookings.AddReviewAsync(id, users.GetUserId(User)!, Rating, Comment);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return Page();
        }
        TempData["Success"] = "Thanks for your review!";
        return RedirectToPage("/Items/Details", new { id = Booking.ItemId });
    }

    private async Task<bool> LoadAsync(int id)
    {
        var userId = users.GetUserId(User);
        var booking = await db.Bookings.AsNoTracking().Include(b => b.Item).Include(b => b.Review)
            .FirstOrDefaultAsync(b => b.Id == id && b.RenterId == userId);
        if (booking is null) return false;
        Booking = booking;
        return true;
    }
}
