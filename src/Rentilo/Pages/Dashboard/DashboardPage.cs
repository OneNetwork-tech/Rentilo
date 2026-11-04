using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;

namespace Rentilo.Pages.Dashboard;

/// <summary>Base page for the dashboard: exposes the user id and the pending-request badge count.</summary>
public abstract class DashboardPage(ApplicationDbContext db) : PageModel
{
    protected ApplicationDbContext Db { get; } = db;

    protected string UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

    public int PendingCount { get; private set; }

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        PendingCount = await Db.Bookings.CountAsync(b => b.Item.OwnerId == UserId && b.Status == BookingStatus.Pending);
        ViewData["PendingCount"] = PendingCount;
        await next();
    }
}
