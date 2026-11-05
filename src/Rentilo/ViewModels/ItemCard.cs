using Rentilo.Models;

namespace Rentilo.ViewModels;

/// <summary>Everything an item card needs, projected in one query.</summary>
public record ItemCard(
    int Id,
    string Title,
    ItemCategory Category,
    decimal PricePerDay,
    string Location,
    string? ImagePath,
    bool IsAvailable,
    bool IsHiddenByAdmin,
    string OwnerName,
    double? Rating,
    int ReviewCount);

public static class ItemQueries
{
    /// <summary>Listings the public may see and book: not paused by the owner and not hidden by a moderator.</summary>
    public static IQueryable<Item> Listed(this IQueryable<Item> items) =>
        items.Where(i => i.IsAvailable && !i.IsHiddenByAdmin);

    public static IQueryable<ItemCard> ToCards(this IQueryable<Item> items) =>
        items.Select(i => new ItemCard(
            i.Id,
            i.Title,
            i.Category,
            i.PricePerDay,
            i.Location,
            i.ImagePath,
            i.IsAvailable,
            i.IsHiddenByAdmin,
            i.Owner.FullName,
            i.Reviews.Average(r => (double?)r.Rating),
            i.Reviews.Count()));
}
