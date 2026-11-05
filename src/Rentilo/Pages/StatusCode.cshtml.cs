using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Rentilo.Pages;

[IgnoreAntiforgeryToken]
public class StatusCodeModel : PageModel
{
    public int Code { get; private set; }
    public string Heading => Code switch { 404 => "Page not found", 403 => "Access denied", _ => "Something went wrong" };
    public string Text => Code switch
    {
        404 => "We couldn't find what you were looking for. It may have been removed.",
        403 => "You don't have permission to view this page.",
        _ => $"The server responded with status {Code}.",
    };

    public void OnGet(int code) => Code = code;

    public void OnPost(int code) => Code = code;
}
