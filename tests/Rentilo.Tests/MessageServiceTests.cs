using Microsoft.Extensions.Logging.Abstractions;
using Rentilo.Models;
using Rentilo.Services;

namespace Rentilo.Tests;

public class MessageServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly RecordingNotifier _notifier = new();
    private readonly MessageService _messages;
    private readonly Booking _booking;

    public MessageServiceTests()
    {
        _messages = new MessageService(_db.Context, _clock, _notifier);
        var bookings = new BookingService(_db.Context, _clock, new NullNotifier(), NullLogger<BookingService>.Instance);
        _booking = bookings.RequestAsync(_db.Item.Id, _db.Renter.Id, new(2026, 6, 20), new(2026, 6, 21), "Hi").Result.Value!;
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Only_participants_can_send()
    {
        Assert.True((await _messages.SendAsync(_booking.Id, _db.Renter.Id, "Can I pick it up at 9?")).Succeeded);
        Assert.True((await _messages.SendAsync(_booking.Id, _db.Owner.Id, "Sure!")).Succeeded);
        Assert.False((await _messages.SendAsync(_booking.Id, _db.Other.Id, "I'm not part of this")).Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_messages_are_rejected(string body) =>
        Assert.False((await _messages.SendAsync(_booking.Id, _db.Renter.Id, body)).Succeeded);

    [Fact]
    public async Task Too_long_messages_are_rejected() =>
        Assert.False((await _messages.SendAsync(_booking.Id, _db.Renter.Id, new string('x', MessageService.MaxLength + 1))).Succeeded);

    [Fact]
    public async Task Declined_conversations_are_closed()
    {
        _booking.Status = BookingStatus.Declined;
        await _db.Context.SaveChangesAsync();
        Assert.False((await _messages.SendAsync(_booking.Id, _db.Renter.Id, "Why?")).Succeeded);
    }

    [Fact]
    public async Task Unread_counts_and_mark_read()
    {
        await _messages.SendAsync(_booking.Id, _db.Renter.Id, "One");
        await _messages.SendAsync(_booking.Id, _db.Renter.Id, "Two");

        Assert.Equal(2, await _messages.UnreadCountAsync(_db.Owner.Id));
        Assert.Equal(0, await _messages.UnreadCountAsync(_db.Renter.Id)); // own messages never count
        Assert.Equal(0, await _messages.UnreadCountAsync(_db.Other.Id));

        Assert.Equal(2, await _messages.MarkReadAsync(_booking.Id, _db.Owner.Id));
        Assert.Equal(0, await _messages.UnreadCountAsync(_db.Owner.Id));
    }

    [Fact]
    public async Task Only_the_first_unread_message_triggers_an_email()
    {
        var first = (await _messages.SendAsync(_booking.Id, _db.Renter.Id, "One")).Value!;
        await _messages.SendAsync(_booking.Id, _db.Renter.Id, "Two");
        Assert.Equal([$"message:{first.Id}"], _notifier.Events);

        await _messages.MarkReadAsync(_booking.Id, _db.Owner.Id);
        var third = (await _messages.SendAsync(_booking.Id, _db.Renter.Id, "Three")).Value!;
        Assert.Equal($"message:{third.Id}", _notifier.Events[^1]);
    }

    [Fact]
    public async Task Conversations_list_shows_last_message_and_unread()
    {
        await _messages.SendAsync(_booking.Id, _db.Renter.Id, "First");
        _clock.Now = _clock.Now.AddMinutes(5);
        await _messages.SendAsync(_booking.Id, _db.Renter.Id, "Latest");

        var list = await _messages.GetConversationsAsync(_db.Owner.Id);

        var c = Assert.Single(list);
        Assert.Equal("Latest", c.Last!.Body);
        Assert.Equal(2, c.Unread);
        Assert.True(c.IAmOwner);
        Assert.Empty(await _messages.GetConversationsAsync(_db.Other.Id));
    }
}
