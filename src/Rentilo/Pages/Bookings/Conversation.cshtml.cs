using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rentilo.Models;
using Rentilo.Services;

namespace Rentilo.Pages.Bookings;

/// <summary>Conversation between renter and owner for one booking. Only the two participants can open it.</summary>
public class ConversationModel(MessageService messages, UserManager<ApplicationUser> users) : PageModel
{
    public Booking Booking { get; private set; } = null!;
    public List<Message> Messages { get; private set; } = [];
    public string MyId { get; private set; } = "";
    public bool IAmOwner => Booking.Item.OwnerId == MyId;
    public ApplicationUser Other => IAmOwner ? Booking.Renter : Booking.Item.Owner;
    public bool IsClosed => Booking.Status == BookingStatus.Declined;

    [BindProperty, Required(ErrorMessage = "Write a message first."), StringLength(MessageService.MaxLength)]
    public string? Body { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        await messages.MarkReadAsync(id, MyId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (!ModelState.IsValid) return Page();

        var result = await messages.SendAsync(id, MyId, Body);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(nameof(Body), result.Error!);
            return Page();
        }
        return Redirect(Url.Page("Conversation", new { id }) + "#latest");
    }

    private async Task<bool> LoadAsync(int id)
    {
        MyId = users.GetUserId(User)!;
        var booking = await messages.GetBookingForParticipantAsync(id, MyId);
        if (booking is null) return false;
        Booking = booking;
        Messages = await messages.GetMessagesAsync(id);
        return true;
    }
}
