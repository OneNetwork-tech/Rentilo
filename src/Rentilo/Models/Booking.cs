using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rentilo.Models;

/// <summary>A rental request from a renter for an item, covering whole days.</summary>
public class Booking
{
    public int Id { get; set; }

    /// <summary>First rental day (inclusive).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last rental day (inclusive).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Total price, always calculated on the server when the booking is created.</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal TotalPrice { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    /// <summary>Optional note from the renter to the owner.</summary>
    [StringLength(500)]
    public string? Message { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public string RenterId { get; set; } = string.Empty;

    public ApplicationUser Renter { get; set; } = null!;

    public Review? Review { get; set; }

    public int Days => EndDate.DayNumber - StartDate.DayNumber + 1;
}

/// <summary>
/// Booking lifecycle. Stored as integers and referenced by the database exclusion constraint
/// (see the InitialCreate migration): never renumber existing values.
/// </summary>
public enum BookingStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Cancelled = 3,
    Completed = 4,
}
