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
    string OwnerName,
    double? Rating,
    int ReviewCount);

public static class ItemQueries
{
    public static IQueryable<ItemCard> ToCards(this IQueryable<Item> items) =>
        items.Select(i => new ItemCard(
            i.Id,
            i.Title,
            i.Category,
            i.PricePerDay,
            i.Location,
            i.ImagePath,
            i.IsAvailable,
            i.Owner.FullName,
            i.Reviews.Average(r => (double?)r.Rating),
            i.Reviews.Count()));
}
