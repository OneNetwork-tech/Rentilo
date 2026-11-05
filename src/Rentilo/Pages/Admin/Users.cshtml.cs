using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.Services;

namespace Rentilo.Pages.Admin;

public class UsersModel(ApplicationDbContext db, ModerationService moderation, TimeProvider clock) : AdminPage
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    public record UserRow(ApplicationUser User, int Listings, int Rentals, bool IsAdmin, bool IsLocked);

    public List<UserRow> Users { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var term = Q.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(term) || u.Email!.ToLower().Contains(term));
        }
        var list = await query.OrderByDescending(u => u.DateJoined).Take(100)
            .Select(u => new { User = u, Listings = u.Items.Count, Rentals = u.BookingsAsRenter.Count })
            .ToListAsync();

        var adminRoleId = await db.Roles.Where(r => r.Name == Roles.Admin).Select(r => r.Id).FirstOrDefaultAsync();
        var admins = adminRoleId is null ? [] : await db.UserRoles.Where(ur => ur.RoleId == adminRoleId).Select(ur => ur.UserId).ToListAsync();
        var now = clock.GetUtcNow();
        Users = list.Select(x => new UserRow(x.User, x.Listings, x.Rentals, admins.Contains(x.User.Id),
            x.User.LockoutEnd is { } end && end > now)).ToList();
    }

    public async Task<IActionResult> OnPostLockAsync(string userId) =>
        Done(await moderation.LockUserAsync(userId, AdminId), "User locked and signed out.", new { Q });

    public async Task<IActionResult> OnPostUnlockAsync(string userId) =>
        Done(await moderation.UnlockUserAsync(userId), "User unlocked.", new { Q });
}
