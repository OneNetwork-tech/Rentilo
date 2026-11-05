using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;

namespace Rentilo.Services;

/// <summary>Conversations between the renter and the owner of a booking.</summary>
public class MessageService(ApplicationDbContext db, TimeProvider clock, INotifier notifier)
{
    public const int MaxLength = 2000;

    /// <summary>Loads a booking the user takes part in (as renter or owner), or null.</summary>
    public Task<Booking?> GetBookingForParticipantAsync(int bookingId, string userId) =>
        db.Bookings
            .Include(b => b.Item).ThenInclude(i => i.Owner)
            .Include(b => b.Renter)
            .FirstOrDefaultAsync(b => b.Id == bookingId && (b.RenterId == userId || b.Item.OwnerId == userId));

    public Task<List<Message>> GetMessagesAsync(int bookingId) =>
        db.Messages.AsNoTracking().Include(m => m.Sender)
            .Where(m => m.BookingId == bookingId)
            .OrderBy(m => m.SentAt).ThenBy(m => m.Id)
            .ToListAsync();

    public async Task<ServiceResult<Message>> SendAsync(int bookingId, string senderId, string? body)
    {
        var text = body?.Trim();
        if (string.IsNullOrEmpty(text)) return ServiceResult<Message>.Fail("Write a message first.");
        if (text.Length > MaxLength) return ServiceResult<Message>.Fail($"Messages can be at most {MaxLength} characters.");

        var booking = await GetBookingForParticipantAsync(bookingId, senderId);
        if (booking is null) return ServiceResult<Message>.Fail("Conversation not found.");
        if (booking.Status is BookingStatus.Declined)
            return ServiceResult<Message>.Fail("This request was declined, so the conversation is closed.");

        // Only email the recipient for the first unread message, so a burst of messages is one email.
        var recipientHasUnread = await db.Messages.AnyAsync(m => m.BookingId == bookingId && m.SenderId == senderId && m.ReadAt == null);

        var message = new Message { BookingId = bookingId, SenderId = senderId, Body = text, SentAt = clock.GetUtcNow().UtcDateTime };
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        if (!recipientHasUnread) await notifier.NewMessageAsync(message.Id);
        return ServiceResult<Message>.Ok(message);
    }

    /// <summary>Marks the other party's messages in this conversation as read by <paramref name="userId"/>.</summary>
    public Task<int> MarkReadAsync(int bookingId, string userId) =>
        db.Messages.Where(m => m.BookingId == bookingId && m.SenderId != userId && m.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ReadAt, clock.GetUtcNow().UtcDateTime));

    public Task<int> UnreadCountAsync(string userId) =>
        db.Messages.CountAsync(m => m.ReadAt == null && m.SenderId != userId &&
                                    (m.Booking.RenterId == userId || m.Booking.Item.OwnerId == userId));

    public record ConversationSummary(Booking Booking, Message? Last, int Unread, bool IAmOwner);

    /// <summary>All of the user's bookings that have messages or are active, newest activity first.</summary>
    public async Task<List<ConversationSummary>> GetConversationsAsync(string userId)
    {
        var bookings = await db.Bookings.AsNoTracking()
            .Include(b => b.Item).ThenInclude(i => i.Owner)
            .Include(b => b.Renter)
            .Where(b => (b.RenterId == userId || b.Item.OwnerId == userId) &&
                        (b.Messages.Any() || b.Status == BookingStatus.Pending || b.Status == BookingStatus.Accepted))
            .ToListAsync();
        var ids = bookings.Select(b => b.Id).ToList();

        var lastMessages = await db.Messages.AsNoTracking()
            .Where(m => ids.Contains(m.BookingId))
            .GroupBy(m => m.BookingId)
            .Select(g => g.OrderByDescending(m => m.SentAt).ThenByDescending(m => m.Id).First())
            .ToListAsync();
        var unread = await db.Messages
            .Where(m => ids.Contains(m.BookingId) && m.ReadAt == null && m.SenderId != userId)
            .GroupBy(m => m.BookingId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return bookings
            .Select(b => new ConversationSummary(b, lastMessages.FirstOrDefault(m => m.BookingId == b.Id),
                unread.GetValueOrDefault(b.Id), b.Item.OwnerId == userId))
            .OrderByDescending(c => c.Last?.SentAt ?? c.Booking.CreatedAt)
            .ToList();
    }
}
