using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.Services;
using Rentilo.ViewModels;

namespace Rentilo.Pages.Items;

[RequestSizeLimit(6 * 1024 * 1024)]
public class EditModel(ApplicationDbContext db, IImageStorage images, UserManager<ApplicationUser> users) : PageModel
{
    [BindProperty] public ItemInput Input { get; set; } = new();
    public int ItemId { get; private set; }
    public string? CurrentImage { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var item = await FindOwnedAsync(id);
        if (item is null) return NotFound();
        ItemId = id;
        CurrentImage = item.ImagePath;
        Input = ItemInput.From(item);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var item = await FindOwnedAsync(id);
        if (item is null) return NotFound();
        ItemId = id;
        CurrentImage = item.ImagePath;
        if (!ModelState.IsValid) return Page();

        var oldImage = item.ImagePath;
        if (Input.Image is not null)
        {
            var saved = await images.SaveAsync(Input.Image, HttpContext.RequestAborted);
            if (!saved.Succeeded)
            {
                ModelState.AddModelError("Input.Image", saved.Error!);
                return Page();
            }
            item.ImagePath = saved.Value;
        }
        else if (Input.RemoveImage)
        {
            item.ImagePath = null;
        }

        Input.ApplyTo(item);
        await db.SaveChangesAsync();
        if (oldImage != item.ImagePath) images.Delete(oldImage);

        TempData["Success"] = "Listing updated.";
        return RedirectToPage("Details", new { id });
    }

    private Task<Item?> FindOwnedAsync(int id)
    {
        var userId = users.GetUserId(User);
        return db.Items.FirstOrDefaultAsync(i => i.Id == id && i.OwnerId == userId);
    }
}
