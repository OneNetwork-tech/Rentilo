using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.Services;

namespace Rentilo.Pages.Dashboard;

/// <summary>Bookings of the current user's items, where the owner accepts, declines and completes them.</summary>
public class RequestsModel(ApplicationDbContext db, BookingService bookings) : DashboardPage(db)
{
    public List<Booking> Pending { get; private set; } = [];
    public List<Booking> Upcoming { get; private set; } = [];
    public List<Booking> History { get; private set; } = [];
    public DateOnly Today => bookings.Today;

    public async Task OnGetAsync()
    {
        var all = await Db.Bookings.AsNoTracking().Include(b => b.Item).Include(b => b.Renter)
            .Where(b => b.Item.OwnerId == UserId)
            .OrderBy(b => b.StartDate).ToListAsync();
        var today = Today;
        Pending = all.Where(b => b.Status == BookingStatus.Pending && b.EndDate >= today).ToList();
        Upcoming = all.Where(b => b.Status == BookingStatus.Accepted).ToList();
        History = all.Except(Pending).Except(Upcoming).OrderByDescending(b => b.StartDate).Take(30).ToList();
    }

    public Task<IActionResult> OnPostAcceptAsync(int bookingId) =>
        Run(bookings.AcceptAsync(bookingId, UserId), "Booking accepted. The renter can now see the confirmed dates.");

    public Task<IActionResult> OnPostDeclineAsync(int bookingId) =>
        Run(bookings.DeclineAsync(bookingId, UserId), "Request declined.");

    public Task<IActionResult> OnPostCompleteAsync(int bookingId) =>
        Run(bookings.CompleteAsync(bookingId, UserId), "Marked as returned. Thanks for sharing!");

    private async Task<IActionResult> Run(Task<ServiceResult> action, string success)
    {
        var result = await action;
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? success : result.Error;
        return RedirectToPage();
    }
}
