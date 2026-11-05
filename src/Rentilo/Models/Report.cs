using System.ComponentModel.DataAnnotations;

namespace Rentilo.Models;

/// <summary>A user's report of a listing that breaks the rules. Handled by admins.</summary>
public class Report
{
    public int Id { get; set; }

    public int ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public string ReporterId { get; set; } = string.Empty;

    public ApplicationUser Reporter { get; set; } = null!;

    public ReportReason Reason { get; set; }

    [StringLength(1000)]
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }

    public string? ResolvedById { get; set; }

    [StringLength(300)]
    public string? Resolution { get; set; }

    public bool IsOpen => ResolvedAt is null;
}

/// <summary>Stored as integers: never renumber existing values.</summary>
public enum ReportReason
{
    [Display(Name = "Prohibited or dangerous item")] Prohibited = 0,
    [Display(Name = "Misleading or wrong information")] Misleading = 1,
    [Display(Name = "Looks like a scam")] Scam = 2,
    [Display(Name = "Offensive content")] Offensive = 3,
    [Display(Name = "Other")] Other = 99,
}
