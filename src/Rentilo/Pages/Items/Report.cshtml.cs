using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.Services;

namespace Rentilo.Pages.Items;

public class ReportModel(ApplicationDbContext db, ModerationService moderation, UserManager<ApplicationUser> users) : PageModel
{
    public Item Item { get; private set; } = null!;

    [BindProperty, Required(ErrorMessage = "Choose a reason.")]
    public ReportReason? Reason { get; set; }

    [BindProperty, StringLength(1000), Display(Name = "Details (optional)")]
    public string? Details { get; set; }

    public async Task<IActionResult> OnGetAsync(int id) => await LoadAsync(id) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!ModelState.IsValid) return Page();

        var result = await moderation.ReportAsync(id, users.GetUserId(User)!, Reason!.Value, Details);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return Page();
        }
        TempData["Success"] = "Thanks. A moderator will review this listing.";
        return RedirectToPage("Details", new { id });
    }

    private async Task<bool> LoadAsync(int id)
    {
        var item = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id && i.IsAvailable && !i.IsHiddenByAdmin);
        if (item is null) return false;
        Item = item;
        return true;
    }
}
