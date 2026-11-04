using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.Services;

namespace Rentilo.Pages.Dashboard;

public class IndexModel(ApplicationDbContext db, BookingService bookings) : DashboardPage(db)
{
    public string FirstName { get; private set; } = "";
    public int ActiveListings { get; private set; }
    public int UpcomingAsOwner { get; private set; }
    public decimal EarnedTotal { get; private set; }
    public int UpcomingAsRenter { get; private set; }
    public List<Booking> NextRentals { get; private set; } = [];
    public List<Booking> LatestRequests { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var today = bookings.Today;
        var name = await Db.Users.Where(u => u.Id == UserId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "";
        FirstName = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "there";

        ActiveListings = await Db.Items.CountAsync(i => i.OwnerId == UserId && i.IsAvailable);
        UpcomingAsOwner = await Db.Bookings.CountAsync(b => b.Item.OwnerId == UserId && b.Status == BookingStatus.Accepted && b.EndDate >= today);
        // Sum client-side: keeps decimal precision on every provider.
        EarnedTotal = (await Db.Bookings
            .Where(b => b.Item.OwnerId == UserId && (b.Status == BookingStatus.Completed || (b.Status == BookingStatus.Accepted && b.EndDate < today)))
            .Select(b => b.TotalPrice).ToListAsync()).Sum();
        UpcomingAsRenter = await Db.Bookings.CountAsync(b => b.RenterId == UserId && b.Status == BookingStatus.Accepted && b.EndDate >= today);

        NextRentals = await Db.Bookings.AsNoTracking().Include(b => b.Item)
            .Where(b => b.RenterId == UserId && (b.Status == BookingStatus.Accepted || b.Status == BookingStatus.Pending) && b.EndDate >= today)
            .OrderBy(b => b.StartDate).Take(5).ToListAsync();

        LatestRequests = await Db.Bookings.AsNoTracking().Include(b => b.Item).Include(b => b.Renter)
            .Where(b => b.Item.OwnerId == UserId && b.Status == BookingStatus.Pending)
            .OrderBy(b => b.StartDate).Take(5).ToListAsync();
    }
}
