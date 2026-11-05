using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rentilo.Models;
using Rentilo.Services;

namespace Rentilo.Pages.Bookings;

public class MessagesModel(MessageService messages, UserManager<ApplicationUser> users) : PageModel
{
    public List<MessageService.ConversationSummary> Conversations { get; private set; } = [];

    public async Task OnGetAsync() => Conversations = await messages.GetConversationsAsync(users.GetUserId(User)!);
}
