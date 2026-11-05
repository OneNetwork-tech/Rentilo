using System.ComponentModel.DataAnnotations;

namespace Rentilo.Models;

/// <summary>A message between the renter and the owner of a booking.</summary>
public class Message
{
    public int Id { get; set; }

    public int BookingId { get; set; }

    public Booking Booking { get; set; } = null!;

    public string SenderId { get; set; } = string.Empty;

    public ApplicationUser Sender { get; set; } = null!;

    [Required, StringLength(2000)]
    public string Body { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the recipient (the other party of the booking) read it; null = unread.</summary>
    public DateTime? ReadAt { get; set; }
}
