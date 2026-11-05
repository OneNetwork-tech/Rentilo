using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.Services;
using Rentilo.ViewModels;

namespace Rentilo.Pages.Items;

[RequestSizeLimit(6 * 1024 * 1024)]
public class CreateModel(ApplicationDbContext db, IImageStorage images, UserManager<ApplicationUser> users) : PageModel
{
    [BindProperty] public ItemInput Input { get; set; } = new();

    public async Task OnGetAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user?.City is { } city) Input.Location = city;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var item = new Item { OwnerId = users.GetUserId(User)! };
        Input.ApplyTo(item);

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

        db.Items.Add(item);
        await db.SaveChangesAsync();
        TempData["Success"] = "Your item is live! 🎉";
        return RedirectToPage("Details", new { id = item.Id });
    }
}
