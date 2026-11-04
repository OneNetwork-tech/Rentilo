using System.ComponentModel.DataAnnotations;

namespace Rentilo.Models;

/// <summary>A renter's review of an item, written after a completed booking. One per booking.</summary>
public class Review
{
    public int Id { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int BookingId { get; set; }

    public Booking Booking { get; set; } = null!;

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public string AuthorId { get; set; } = string.Empty;

    public ApplicationUser Author { get; set; } = null!;
}
