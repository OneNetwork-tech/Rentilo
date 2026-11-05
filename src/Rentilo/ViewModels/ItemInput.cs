using System.ComponentModel.DataAnnotations;
using Rentilo.Models;

namespace Rentilo.ViewModels;

/// <summary>Form model for creating and editing listings (never bind the entity directly: prevents over-posting OwnerId etc.).</summary>
public class ItemInput
{
    [Required, StringLength(120, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(2000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    public ItemCategory Category { get; set; } = ItemCategory.Other;

    [Range(1, 100_000, ErrorMessage = "Price per day must be between 1 and 100 000 kr.")]
    [Display(Name = "Price per day (kr)")]
    public decimal PricePerDay { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Location", Prompt = "e.g. Stockholm, Södermalm")]
    public string Location { get; set; } = string.Empty;

    [Display(Name = "Available for rent")]
    public bool IsAvailable { get; set; } = true;

    [Display(Name = "Photo")]
    public IFormFile? Image { get; set; }

    [Display(Name = "Remove current photo")]
    public bool RemoveImage { get; set; }

    public void ApplyTo(Item item)
    {
        item.Title = Title.Trim();
        item.Description = Description.Trim();
        item.Category = Category;
        item.PricePerDay = Math.Round(PricePerDay, 2);
        item.Location = Location.Trim();
        item.IsAvailable = IsAvailable;
    }

    public static ItemInput From(Item item) => new()
    {
        Title = item.Title,
        Description = item.Description,
        Category = item.Category,
        PricePerDay = item.PricePerDay,
        Location = item.Location,
        IsAvailable = item.IsAvailable,
    };
}

/// <summary>Model for the shared _ItemForm partial. Property is named Input so field names match the page's [BindProperty].</summary>
public record ItemFormModel(ItemInput Input, string? CurrentImage, string SubmitText);
