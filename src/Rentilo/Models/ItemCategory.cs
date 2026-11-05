using System.ComponentModel.DataAnnotations;

namespace Rentilo.Models;

/// <summary>Item categories. Stored as integers: never renumber existing values.</summary>
public enum ItemCategory
{
    [Display(Name = "Tools & DIY")] Tools = 0,
    [Display(Name = "Electronics")] Electronics = 1,
    [Display(Name = "Outdoor & Camping")] Outdoor = 2,
    [Display(Name = "Sports")] Sports = 3,
    [Display(Name = "Party & Events")] Party = 4,
    [Display(Name = "Vehicles & Bikes")] Vehicles = 5,
    [Display(Name = "Home & Garden")] Home = 6,
    [Display(Name = "Photo & Video")] Photography = 7,
    [Display(Name = "Music")] Music = 8,
    [Display(Name = "Other")] Other = 99,
}
