using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.Services;

namespace Rentilo.Pages.Admin;

public class ReviewsModel(ApplicationDbContext db, ModerationService moderation) : AdminPage
{
    public List<Review> Reviews { get; private set; } = [];

    public async Task OnGetAsync() =>
        Reviews = await db.Reviews.AsNoTracking().Include(r => r.Author).Include(r => r.Item)
            .OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync();

    public async Task<IActionResult> OnPostDeleteAsync(int reviewId) =>
        Done(await moderation.DeleteReviewAsync(reviewId, AdminId), "Review deleted.");
}
