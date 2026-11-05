using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.Services;

namespace Rentilo.Pages.Admin;

public class IndexModel(ApplicationDbContext db, ModerationService moderation) : AdminPage
{
    public record ReportGroup(Item Item, List<Report> Reports);

    public List<ReportGroup> Open { get; private set; } = [];
    public List<Report> RecentlyResolved { get; private set; } = [];
    public int UserCount { get; private set; }
    public int ItemCount { get; private set; }
    public int BookingCount { get; private set; }
    public int HiddenCount { get; private set; }

    public async Task OnGetAsync()
    {
        UserCount = await db.Users.CountAsync();
        ItemCount = await db.Items.CountAsync();
        HiddenCount = await db.Items.CountAsync(i => i.IsHiddenByAdmin);
        BookingCount = await db.Bookings.CountAsync();

        var open = await db.Reports.AsNoTracking()
            .Include(r => r.Item).ThenInclude(i => i.Owner)
            .Include(r => r.Reporter)
            .Where(r => r.ResolvedAt == null)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
        Open = open.GroupBy(r => r.ItemId).Select(g => new ReportGroup(g.First().Item, g.ToList()))
            .OrderByDescending(g => g.Reports.Count).ToList();

        RecentlyResolved = await db.Reports.AsNoTracking().Include(r => r.Item)
            .Where(r => r.ResolvedAt != null).OrderByDescending(r => r.ResolvedAt).Take(10).ToListAsync();
    }

    public async Task<IActionResult> OnPostHideAsync(int itemId, string? note) =>
        Done(await moderation.HideItemAsync(itemId, AdminId, note), "Listing hidden and reports closed.");

    public async Task<IActionResult> OnPostDismissAsync(int itemId)
    {
        await moderation.ResolveReportsAsync(itemId, AdminId, "Dismissed – no violation");
        return Done(ServiceResult.Ok(), "Reports dismissed.");
    }
}
