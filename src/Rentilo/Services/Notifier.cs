using System.Text.Encodings.Web;
using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;

namespace Rentilo.Services;

/// <summary>
/// Sends the transactional emails for bookings and messages. All user-provided text is HTML-encoded.
/// Links are absolute, built from the current request (or <c>App:BaseUrl</c> outside a request).
/// </summary>
public interface INotifier
{
    Task BookingRequestedAsync(int bookingId);
    Task BookingAcceptedAsync(int bookingId);
    Task BookingDeclinedAsync(int bookingId, bool automatically = false);
    Task BookingCancelledAsync(int bookingId);
    Task NewMessageAsync(int messageId);
}

public class EmailNotifier(
    ApplicationDbContext db,
    IEmailQueue queue,
    LinkGenerator links,
    IHttpContextAccessor http,
    IConfiguration config) : INotifier
{
    private static readonly HtmlEncoder Html = HtmlEncoder.Default;

    public async Task BookingRequestedAsync(int bookingId)
    {
        var b = await LoadAsync(bookingId);
        if (b is null) return;
        var body = $"""
            <p><strong>{E(b.Renter.FullName)}</strong> wants to rent <strong>{E(b.Item.Title)}</strong>.</p>
            {Summary(b)}
            {(string.IsNullOrEmpty(b.Message) ? "" : $"<p style=\"padding:12px;background:#f6f8f7;border-radius:8px\">“{E(b.Message)}”</p>")}
            {Button("Accept or decline", Url("/Dashboard/Requests"))}
            """;
        Send(b.Item.Owner, $"New request for {b.Item.Title}", "You have a new booking request", body);
    }

    public async Task BookingAcceptedAsync(int bookingId)
    {
        var b = await LoadAsync(bookingId);
        if (b is null) return;
        var body = $"""
            <p>Good news! <strong>{E(b.Item.Owner.FullName)}</strong> accepted your request for <strong>{E(b.Item.Title)}</strong>.</p>
            {Summary(b)}
            <p>Pick-up area: {E(b.Item.Location)}. Send a message to agree on a time.</p>
            {Button("Message the owner", Url("/Bookings/Conversation", new { id = b.Id }))}
            """;
        Send(b.Renter, $"Booking confirmed: {b.Item.Title}", "Your booking is confirmed", body);
    }

    public async Task BookingDeclinedAsync(int bookingId, bool automatically = false)
    {
        var b = await LoadAsync(bookingId);
        if (b is null) return;
        var reason = automatically
            ? "Those dates were booked by someone else."
            : $"{E(b.Item.Owner.FullName)} couldn't accept it this time.";
        var body = $"""
            <p>Your request for <strong>{E(b.Item.Title)}</strong> ({E(Display.DateRange(b.StartDate, b.EndDate))}) was declined. {reason}</p>
            {Button("Find something similar", Url("/Items/Index", new { category = b.Item.Category.ToString() }))}
            """;
        Send(b.Renter, $"Request declined: {b.Item.Title}", "Your request was declined", body);
    }

    public async Task BookingCancelledAsync(int bookingId)
    {
        var b = await LoadAsync(bookingId);
        if (b is null) return;
        var body = $"""
            <p><strong>{E(b.Renter.FullName)}</strong> cancelled their booking of <strong>{E(b.Item.Title)}</strong>.</p>
            {Summary(b)}
            <p>Those dates are free again.</p>
            {Button("Open your dashboard", Url("/Dashboard/Index"))}
            """;
        Send(b.Item.Owner, $"Booking cancelled: {b.Item.Title}", "A booking was cancelled", body);
    }

    public async Task NewMessageAsync(int messageId)
    {
        var m = await db.Messages.AsNoTracking()
            .Include(x => x.Sender)
            .Include(x => x.Booking).ThenInclude(b => b.Renter)
            .Include(x => x.Booking).ThenInclude(b => b.Item).ThenInclude(i => i.Owner)
            .FirstOrDefaultAsync(x => x.Id == messageId);
        if (m is null) return;
        var recipient = m.SenderId == m.Booking.RenterId ? m.Booking.Item.Owner : m.Booking.Renter;
        var body = $"""
            <p><strong>{E(m.Sender.FullName)}</strong> sent you a message about <strong>{E(m.Booking.Item.Title)}</strong>:</p>
            <p style="padding:12px;background:#f6f8f7;border-radius:8px;white-space:pre-line">{E(m.Body)}</p>
            {Button("Reply", Url("/Bookings/Conversation", new { id = m.BookingId }))}
            """;
        Send(recipient, $"New message about {m.Booking.Item.Title}", "You have a new message", body);
    }

    private Task<Booking?> LoadAsync(int bookingId) =>
        db.Bookings.AsNoTracking()
            .Include(b => b.Renter)
            .Include(b => b.Item).ThenInclude(i => i.Owner)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

    private void Send(ApplicationUser to, string subject, string heading, string content)
    {
        if (string.IsNullOrEmpty(to.Email)) return;
        queue.Enqueue(new EmailMessage(to.Email, subject, Layout(to.FullName, heading, content)));
    }

    private static string E(string? text) => Html.Encode(text ?? "");

    private static string Summary(Booking b) =>
        $"""<p style="color:#5c6b66">{E(Display.DateRange(b.StartDate, b.EndDate))} · {E(Display.Days(b.Days))} · <strong style="color:#17221e">{E(Display.Money(b.TotalPrice))}</strong></p>""";

    private static string Button(string text, string url) =>
        $"""<p style="margin:24px 0"><a href="{Html.Encode(url)}" style="background:#1f7a63;color:#fff;padding:12px 20px;border-radius:10px;text-decoration:none;font-weight:600">{E(text)}</a></p>""";

    private string Url(string page, object? values = null)
    {
        var context = http.HttpContext;
        if (context is not null)
            return links.GetUriByPage(context, page, handler: null, values: values) ?? page;
        var baseUrl = (config["App:BaseUrl"] ?? "http://localhost:8080").TrimEnd('/');
        return baseUrl + (links.GetPathByPage(page, handler: null, values: values) ?? page);
    }

    private static string Layout(string name, string heading, string content) => $"""
        <!DOCTYPE html>
        <html><body style="margin:0;background:#f6f8f7;font-family:-apple-system,Segoe UI,Roboto,Arial,sans-serif;color:#17221e">
          <div style="max-width:560px;margin:0 auto;padding:24px">
            <p style="font-size:20px;font-weight:800;margin:0 0 16px"><span style="display:inline-block;width:28px;height:28px;line-height:28px;text-align:center;border-radius:8px;background:#1f7a63;color:#fff">R</span> Rentilo</p>
            <div style="background:#fff;border:1px solid #e3e8e6;border-radius:14px;padding:24px">
              <h1 style="font-size:20px;margin:0 0 12px">{E(heading)}</h1>
              <p>Hi {E(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? name)},</p>
              {content}
            </div>
            <p style="color:#5c6b66;font-size:12px;margin-top:16px">You're receiving this because you have an account on Rentilo.</p>
          </div>
        </body></html>
        """;
}

/// <summary>Used in tests and when notifications are not wanted.</summary>
public sealed class NullNotifier : INotifier
{
    public Task BookingRequestedAsync(int bookingId) => Task.CompletedTask;
    public Task BookingAcceptedAsync(int bookingId) => Task.CompletedTask;
    public Task BookingDeclinedAsync(int bookingId, bool automatically = false) => Task.CompletedTask;
    public Task BookingCancelledAsync(int bookingId) => Task.CompletedTask;
    public Task NewMessageAsync(int messageId) => Task.CompletedTask;
}
