using Microsoft.EntityFrameworkCore;
using Rentilo.Data;
using Rentilo.Models;

namespace Rentilo.Services;

/// <summary>
/// All booking rules live here so pages stay thin and the rules are unit-testable.
/// <list type="bullet">
/// <item>Prices are always calculated on the server: days (inclusive) × item price per day.</item>
/// <item>Pending requests may overlap each other; only <see cref="BookingStatus.Accepted"/> and
/// <see cref="BookingStatus.Completed"/> bookings block dates. Accepting a request automatically
/// declines other pending requests that overlap it.</item>
/// <item>On PostgreSQL an exclusion constraint additionally guarantees no two accepted bookings overlap,
/// even under concurrent requests.</item>
/// </list>
/// </summary>
public class BookingService(ApplicationDbContext db, TimeProvider clock, INotifier notifier, ILogger<BookingService> logger)
{
    public const int MaxRentalDays = 60;
    public const int MaxDaysInAdvance = 365;

    /// <summary>Statuses that occupy the item's calendar.</summary>
    public static readonly BookingStatus[] BlockingStatuses = [BookingStatus.Accepted, BookingStatus.Completed];

    public DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    public static decimal CalculatePrice(decimal pricePerDay, DateOnly start, DateOnly end)
    {
        var days = end.DayNumber - start.DayNumber + 1;
        if (days < 1) throw new ArgumentException("End date must be on or after start date.");
        return Math.Round(pricePerDay * days, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Validates dates for a new request. Returns an error message or null.</summary>
    public string? ValidateDates(DateOnly start, DateOnly end)
    {
        if (start < Today) return "The rental can't start in the past.";
        if (end < start) return "The end date must be on or after the start date.";
        if (end.DayNumber - start.DayNumber + 1 > MaxRentalDays) return $"A rental can be at most {MaxRentalDays} days.";
        if (start.DayNumber - Today.DayNumber > MaxDaysInAdvance) return "You can book at most one year ahead.";
        return null;
    }

    public Task<bool> HasConflictAsync(int itemId, DateOnly start, DateOnly end, int? excludeBookingId = null) =>
        db.Bookings.AnyAsync(b =>
            b.ItemId == itemId &&
            BlockingStatuses.Contains(b.Status) &&
            b.Id != (excludeBookingId ?? 0) &&
            b.StartDate <= end && start <= b.EndDate);

    /// <summary>Upcoming date ranges that are already taken for an item (for display on the item page).</summary>
    public Task<List<(DateOnly Start, DateOnly End)>> GetBlockedRangesAsync(int itemId) =>
        db.Bookings
            .Where(b => b.ItemId == itemId && BlockingStatuses.Contains(b.Status) && b.EndDate >= Today)
            .OrderBy(b => b.StartDate)
            .Select(b => new ValueTuple<DateOnly, DateOnly>(b.StartDate, b.EndDate))
            .ToListAsync();

    public async Task<ServiceResult<Booking>> RequestAsync(int itemId, string renterId, DateOnly start, DateOnly end, string? message)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId);
        if (item is null) return ServiceResult<Booking>.Fail("This item no longer exists.");
        if (!item.IsListed) return ServiceResult<Booking>.Fail("This item is not available for rent right now.");
        if (item.OwnerId == renterId) return ServiceResult<Booking>.Fail("You can't rent your own item.");

        var dateError = ValidateDates(start, end);
        if (dateError is not null) return ServiceResult<Booking>.Fail(dateError);

        if (await HasConflictAsync(itemId, start, end))
            return ServiceResult<Booking>.Fail("Those dates are already booked. Please pick other dates.");

        var duplicate = await db.Bookings.AnyAsync(b =>
            b.ItemId == itemId && b.RenterId == renterId && b.Status == BookingStatus.Pending &&
            b.StartDate <= end && start <= b.EndDate);
        if (duplicate) return ServiceResult<Booking>.Fail("You already have a pending request for these dates.");

        var booking = new Booking
        {
            ItemId = itemId,
            RenterId = renterId,
            StartDate = start,
            EndDate = end,
            TotalPrice = CalculatePrice(item.PricePerDay, start, end),
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            Status = BookingStatus.Pending,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        logger.LogInformation("Booking {BookingId} requested for item {ItemId}", booking.Id, itemId);
        await notifier.BookingRequestedAsync(booking.Id);
        return ServiceResult<Booking>.Ok(booking);
    }

    public async Task<ServiceResult> AcceptAsync(int bookingId, string ownerId)
    {
        var booking = await LoadForOwnerAsync(bookingId, ownerId);
        if (booking is null) return ServiceResult.Fail("Booking not found.");
        if (booking.Status != BookingStatus.Pending) return ServiceResult.Fail("Only pending requests can be accepted.");
        if (booking.EndDate < Today) return ServiceResult.Fail("This request is for dates that have already passed.");

        if (await HasConflictAsync(booking.ItemId, booking.StartDate, booking.EndDate, booking.Id))
            return ServiceResult.Fail("These dates overlap an already accepted booking.");

        booking.Status = BookingStatus.Accepted;
        booking.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        var overlappingPending = await db.Bookings
            .Where(b => b.ItemId == booking.ItemId && b.Id != booking.Id && b.Status == BookingStatus.Pending &&
                        b.StartDate <= booking.EndDate && booking.StartDate <= b.EndDate)
            .ToListAsync();
        foreach (var other in overlappingPending)
        {
            other.Status = BookingStatus.Declined;
            other.UpdatedAt = booking.UpdatedAt;
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Raised by the PostgreSQL exclusion constraint when a concurrent accept won the race.
            logger.LogWarning(ex, "Conflict accepting booking {BookingId}", bookingId);
            return ServiceResult.Fail("These dates were just taken by another booking.");
        }

        await notifier.BookingAcceptedAsync(booking.Id);
        foreach (var other in overlappingPending)
            await notifier.BookingDeclinedAsync(other.Id, automatically: true);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeclineAsync(int bookingId, string ownerId)
    {
        var booking = await LoadForOwnerAsync(bookingId, ownerId);
        if (booking is null) return ServiceResult.Fail("Booking not found.");
        if (booking.Status != BookingStatus.Pending) return ServiceResult.Fail("Only pending requests can be declined.");
        var result = await SetStatusAsync(booking, BookingStatus.Declined);
        await notifier.BookingDeclinedAsync(booking.Id);
        return result;
    }

    public async Task<ServiceResult> CompleteAsync(int bookingId, string ownerId)
    {
        var booking = await LoadForOwnerAsync(bookingId, ownerId);
        if (booking is null) return ServiceResult.Fail("Booking not found.");
        if (booking.Status != BookingStatus.Accepted) return ServiceResult.Fail("Only accepted bookings can be completed.");
        if (booking.StartDate > Today) return ServiceResult.Fail("A booking can't be completed before it starts.");
        return await SetStatusAsync(booking, BookingStatus.Completed);
    }

    /// <summary>Renter withdraws a request, or cancels an accepted booking before it starts.</summary>
    public async Task<ServiceResult> CancelAsync(int bookingId, string renterId)
    {
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId && b.RenterId == renterId);
        if (booking is null) return ServiceResult.Fail("Booking not found.");
        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Accepted))
            return ServiceResult.Fail("This booking can no longer be cancelled.");
        if (booking.Status == BookingStatus.Accepted && booking.StartDate <= Today)
            return ServiceResult.Fail("A booking that has already started can't be cancelled.");
        var result = await SetStatusAsync(booking, BookingStatus.Cancelled);
        await notifier.BookingCancelledAsync(booking.Id);
        return result;
    }

    public bool CanReview(Booking booking) =>
        booking.Review is null &&
        (booking.Status == BookingStatus.Completed ||
         (booking.Status == BookingStatus.Accepted && booking.EndDate < Today));

    public async Task<ServiceResult> AddReviewAsync(int bookingId, string renterId, int rating, string? comment)
    {
        if (rating is < 1 or > 5) return ServiceResult.Fail("Rating must be between 1 and 5.");
        var booking = await db.Bookings.Include(b => b.Review)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.RenterId == renterId);
        if (booking is null) return ServiceResult.Fail("Booking not found.");
        if (!CanReview(booking)) return ServiceResult.Fail("You can review an item once your rental has ended.");

        db.Reviews.Add(new Review
        {
            BookingId = booking.Id,
            ItemId = booking.ItemId,
            AuthorId = renterId,
            Rating = rating,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private Task<Booking?> LoadForOwnerAsync(int bookingId, string ownerId) =>
        db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId && b.Item.OwnerId == ownerId);

    private async Task<ServiceResult> SetStatusAsync(Booking booking, BookingStatus status)
    {
        booking.Status = status;
        booking.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }
}
