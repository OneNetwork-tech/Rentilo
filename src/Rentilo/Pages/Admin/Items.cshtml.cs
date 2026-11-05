using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.Services;

namespace Rentilo.Pages.Admin;

public class ItemsModel(ApplicationDbContext db, ModerationService moderation) : AdminPage
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public bool HiddenOnly { get; set; }

    public List<Item> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var query = db.Items.AsNoTracking().Include(i => i.Owner).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var term = Q.Trim().ToLower();
            query = query.Where(i => i.Title.ToLower().Contains(term) || i.Owner.FullName.ToLower().Contains(term) || i.Owner.Email!.ToLower().Contains(term));
        }
        if (HiddenOnly) query = query.Where(i => i.IsHiddenByAdmin);
        Items = await query.OrderByDescending(i => i.DateListed).Take(100).ToListAsync();
    }

    public async Task<IActionResult> OnPostHideAsync(int itemId, string? note, string? returnUrl) =>
        Back(Done(await moderation.HideItemAsync(itemId, AdminId, note), "Listing hidden.", new { Q, HiddenOnly }), returnUrl);

    public async Task<IActionResult> OnPostUnhideAsync(int itemId, string? returnUrl) =>
        Back(Done(await moderation.UnhideItemAsync(itemId), "Listing visible again.", new { Q, HiddenOnly }), returnUrl);

    private IActionResult Back(IActionResult fallback, string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : fallback;
}
