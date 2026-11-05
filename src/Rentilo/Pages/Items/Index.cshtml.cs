using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.ViewModels;

namespace Rentilo.Pages.Items;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    public const int PageSize = 12;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Location { get; set; }
    [BindProperty(SupportsGet = true)] public ItemCategory? Category { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? MinPrice { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? MaxPrice { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    public List<ItemCard> Items { get; private set; } = [];
    public Pager Pager { get; private set; } = null!;
    public bool HasFilters => !string.IsNullOrWhiteSpace(Q) || !string.IsNullOrWhiteSpace(Location) || Category.HasValue || MinPrice.HasValue || MaxPrice.HasValue;

    public async Task OnGetAsync()
    {
        var query = db.Items.AsNoTracking().Listed();

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var term = Q.Trim().ToLower();
            query = query.Where(i => i.Title.ToLower().Contains(term) || i.Description.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(Location))
        {
            var loc = Location.Trim().ToLower();
            query = query.Where(i => i.Location.ToLower().Contains(loc));
        }
        if (Category.HasValue) query = query.Where(i => i.Category == Category);
        if (MinPrice.HasValue) query = query.Where(i => i.PricePerDay >= MinPrice);
        if (MaxPrice.HasValue) query = query.Where(i => i.PricePerDay <= MaxPrice);

        query = Sort switch
        {
            "price" => query.OrderBy(i => i.PricePerDay).ThenByDescending(i => i.Id),
            "price_desc" => query.OrderByDescending(i => i.PricePerDay).ThenByDescending(i => i.Id),
            "rating" => query.OrderByDescending(i => i.Reviews.Average(r => (double?)r.Rating) ?? 0).ThenByDescending(i => i.Id),
            _ => query.OrderByDescending(i => i.DateListed).ThenByDescending(i => i.Id),
        };

        var total = await query.CountAsync();
        var pager = new Pager(1, PageSize, total, RouteValues());
        PageNumber = Math.Clamp(PageNumber, 1, pager.TotalPages);
        Pager = pager with { Page = PageNumber };

        Items = await query.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToCards().ToListAsync();
    }

    private Dictionary<string, string?> RouteValues() => new()
    {
        ["q"] = Q,
        ["location"] = Location,
        ["category"] = Category?.ToString(),
        ["minPrice"] = MinPrice?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["maxPrice"] = MaxPrice?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["sort"] = Sort,
    };
}
