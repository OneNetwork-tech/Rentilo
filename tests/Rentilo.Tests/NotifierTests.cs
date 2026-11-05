using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Rentilo.Data;
using Rentilo.Models;
using Rentilo.Services;

namespace Rentilo.Tests;

public class NotifierTests(RentiloFactory factory) : IClassFixture<RentiloFactory>
{
    private sealed class CapturingQueue : IEmailQueue
    {
        public List<EmailMessage> Sent { get; } = [];
        public void Enqueue(EmailMessage message) => Sent.Add(message);
    }

    [Fact]
    public async Task Booking_request_email_goes_to_owner_and_encodes_user_text()
    {
        using var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var queue = new CapturingQueue();
        var notifier = new EmailNotifier(db, queue, sp.GetRequiredService<LinkGenerator>(), sp.GetRequiredService<IHttpContextAccessor>(), sp.GetRequiredService<IConfiguration>());

        var owner = await users.FindByEmailAsync("erik@rentilo.dev");
        var renter = await users.FindByEmailAsync("sara@rentilo.dev");
        var item = db.Items.First(i => i.OwnerId == owner!.Id);
        var booking = new Booking
        {
            ItemId = item.Id, RenterId = renter!.Id, StartDate = new(2030, 1, 1), EndDate = new(2030, 1, 2),
            TotalPrice = 100, Message = "<script>alert('x')</script>",
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        await notifier.BookingRequestedAsync(booking.Id);

        var mail = Assert.Single(queue.Sent);
        Assert.Equal("erik@rentilo.dev", mail.To);
        Assert.Contains(item.Title, mail.Subject);
        Assert.DoesNotContain("<script>", mail.HtmlBody);
        Assert.Contains("&lt;script&gt;", mail.HtmlBody);
        Assert.Contains("/Dashboard/Requests", mail.HtmlBody);
    }
}
