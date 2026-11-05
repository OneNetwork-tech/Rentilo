using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.ViewModels;

namespace Rentilo.Pages.Dashboard;

public class ListingsModel(ApplicationDbContext db) : DashboardPage(db)
{
    public List<ItemCard> Items { get; private set; } = [];
    public Dictionary<int, int> PendingByItem { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Items = await Db.Items.AsNoTracking().Where(i => i.OwnerId == UserId)
            .OrderByDescending(i => i.IsAvailable).ThenByDescending(i => i.DateListed)
            .ToCards().ToListAsync();
        PendingByItem = await Db.Bookings.Where(b => b.Item.OwnerId == UserId && b.Status == BookingStatus.Pending)
            .GroupBy(b => b.ItemId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }
}
