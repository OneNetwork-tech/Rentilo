using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rentilo.Services;

namespace Rentilo.Pages.Admin;

/// <summary>Base for admin pages (the /Admin folder requires the Admin role, see Program.cs).</summary>
public abstract class AdminPage : PageModel
{
    protected string AdminId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

    protected IActionResult Done(ServiceResult result, string success, object? routeValues = null)
    {
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? success : result.Error;
        return RedirectToPage(routeValues);
    }
}
