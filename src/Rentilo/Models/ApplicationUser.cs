using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Rentilo.Models;

/// <summary>
/// A Rentilo user. Every user can both list items (as an owner) and rent items (as a renter).
/// </summary>
public class ApplicationUser : IdentityUser
{
    [PersonalData]
    [Required]
    [StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [PersonalData]
    [StringLength(100)]
    public string? City { get; set; }

    [PersonalData]
    [StringLength(500)]
    public string? Bio { get; set; }

    public DateTime DateJoined { get; set; } = DateTime.UtcNow;

    public ICollection<Item> Items { get; set; } = new List<Item>();

    public ICollection<Booking> BookingsAsRenter { get; set; } = new List<Booking>();

    /// <summary>Initials used for the avatar placeholder, e.g. "Anna Svensson" → "AS".</summary>
    public string Initials => GetInitials(FullName);

    public static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var first = char.ToUpperInvariant(parts[0][0]);
        return parts.Length == 1 ? first.ToString() : $"{first}{char.ToUpperInvariant(parts[^1][0])}";
    }
}
