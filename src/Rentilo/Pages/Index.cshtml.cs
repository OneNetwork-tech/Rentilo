using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.ViewModels;

namespace Rentilo.Pages;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    public List<ItemCard> Latest { get; private set; } = [];
    public int ItemCount { get; private set; }
    public int CityCount { get; private set; }

    public async Task OnGetAsync()
    {
        var available = db.Items.AsNoTracking().Where(i => i.IsAvailable);
        Latest = await available.OrderByDescending(i => i.DateListed).ToCards().Take(8).ToListAsync();
        ItemCount = await available.CountAsync();
        CityCount = await db.Users.Where(u => u.City != null).Select(u => u.City).Distinct().CountAsync();
    }
}
