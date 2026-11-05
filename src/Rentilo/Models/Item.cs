using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rentilo.Models;

/// <summary>An item a user has listed for rent.</summary>
public class Item
{
    public int Id { get; set; }

    [Required]
    [StringLength(120, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(2000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    public ItemCategory Category { get; set; } = ItemCategory.Other;

    [Column(TypeName = "numeric(10,2)")]
    [Range(1, 100_000)]
    [Display(Name = "Price per day")]
    public decimal PricePerDay { get; set; }

    /// <summary>Site-relative path of the uploaded image (e.g. "/uploads/items/abc.jpg"), or null.</summary>
    [StringLength(300)]
    public string? ImagePath { get; set; }

    [Required]
    [StringLength(100)]
    public string Location { get; set; } = string.Empty;

    public DateTime DateListed { get; set; } = DateTime.UtcNow;

    /// <summary>When false the listing is hidden from search and cannot be booked.</summary>
    [Display(Name = "Available for rent")]
    public bool IsAvailable { get; set; } = true;

    /// <summary>Set by a moderator: the listing is hidden from everyone but its owner and can't be booked.</summary>
    public bool IsHiddenByAdmin { get; set; }

    /// <summary>Moderator's reason, shown to the owner.</summary>
    [StringLength(300)]
    public string? ModerationNote { get; set; }

    /// <summary>Visible in search and bookable.</summary>
    public bool IsListed => IsAvailable && !IsHiddenByAdmin;

    public string OwnerId { get; set; } = string.Empty;

    public ApplicationUser Owner { get; set; } = null!;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public ICollection<Review> Reviews { get; set; } = new List<Review>();

    public ICollection<Report> Reports { get; set; } = new List<Report>();
}
