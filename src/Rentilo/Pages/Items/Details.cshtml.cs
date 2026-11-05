using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.Services;
using Rentilo.ViewModels;

namespace Rentilo.Pages.Items;

public class DetailsModel(ApplicationDbContext db, BookingService bookings, UserManager<ApplicationUser> users) : PageModel
{
    public Item Item { get; private set; } = null!;
    public List<Review> Reviews { get; private set; } = [];
    public double? AverageRating { get; private set; }
    public List<(DateOnly Start, DateOnly End)> BlockedRanges { get; private set; } = [];
    public int OwnerItemCount { get; private set; }
    public bool IsOwner { get; private set; }
    public bool IsAdmin => User.IsInRole(Roles.Admin);
    public DateOnly Today => bookings.Today;

    [BindProperty] public BookingInput Input { get; set; } = new();

    public class BookingInput
    {
        [Required, DataType(DataType.Date), Display(Name = "From")]
        public DateOnly? StartDate { get; set; }

        [Required, DataType(DataType.Date), Display(Name = "To")]
        public DateOnly? EndDate { get; set; }

        [StringLength(500), Display(Name = "Message to the owner (optional)")]
        public string? Message { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        // Paused or moderated listings are only visible to their owner and admins.
        if (!Item.IsListed && !IsOwner && !IsAdmin) return NotFound();
        Input.StartDate ??= Today.AddDays(1);
        Input.EndDate ??= Today.AddDays(2);
        return Page();
    }

    public async Task<IActionResult> OnPostBookAsync(int id)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Challenge();

        if (!await LoadAsync(id)) return NotFound();
        if (!ModelState.IsValid) return Page();

        var result = await bookings.RequestAsync(id, users.GetUserId(User)!, Input.StartDate!.Value, Input.EndDate!.Value, Input.Message);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return Page();
        }

        TempData["Success"] = $"Request sent! {Item.Owner.FullName.Split(' ')[0]} will get back to you soon.";
        return RedirectToPage("/Bookings/Index");
    }

    private async Task<bool> LoadAsync(int id)
    {
        var item = await db.Items.AsNoTracking().Include(i => i.Owner).FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return false;
        Item = item;
        IsOwner = users.GetUserId(User) == item.OwnerId;
        Reviews = await db.Reviews.AsNoTracking().Include(r => r.Author)
            .Where(r => r.ItemId == id).OrderByDescending(r => r.CreatedAt).Take(20).ToListAsync();
        AverageRating = await db.Reviews.Where(r => r.ItemId == id).AverageAsync(r => (double?)r.Rating);
        BlockedRanges = await bookings.GetBlockedRangesAsync(id);
        OwnerItemCount = await db.Items.Listed().CountAsync(i => i.OwnerId == item.OwnerId);
        return true;
    }
}
