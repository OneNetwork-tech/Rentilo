using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Rentilo.Models;
using Rentilo.Services;

namespace Rentilo.Tests;

public class BookingServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 6, 15);
    private readonly TestDb _db = new();
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly BookingService _service;

    public BookingServiceTests() =>
        _service = new BookingService(_db.Context, _clock, NullLogger<BookingService>.Instance);

    public void Dispose() => _db.Dispose();

    private Task<ServiceResult<Booking>> Request(int fromDay, int toDay, string? renter = null) =>
        _service.RequestAsync(_db.Item.Id, renter ?? _db.Renter.Id, Today.AddDays(fromDay), Today.AddDays(toDay), null);

    [Theory]
    [InlineData(0, 0, 100)]   // same day = 1 day
    [InlineData(0, 2, 300)]   // inclusive
    [InlineData(0, 6, 700)]
    public void CalculatePrice_counts_days_inclusively(int fromDay, int toDay, decimal expected) =>
        Assert.Equal(expected, BookingService.CalculatePrice(100, Today.AddDays(fromDay), Today.AddDays(toDay)));

    [Fact]
    public void CalculatePrice_rejects_inverted_range() =>
        Assert.Throws<ArgumentException>(() => BookingService.CalculatePrice(100, Today, Today.AddDays(-1)));

    [Fact]
    public async Task Request_creates_pending_booking_with_server_side_price()
    {
        var result = await Request(1, 3);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(BookingStatus.Pending, result.Value!.Status);
        Assert.Equal(300m, result.Value.TotalPrice);
    }

    [Theory]
    [InlineData(-1, 1, "past")]
    [InlineData(3, 1, "on or after")]
    [InlineData(1, 61, "at most 60 days")]
    [InlineData(366, 367, "one year")]
    public async Task Request_validates_dates(int fromDay, int toDay, string expectedError)
    {
        var result = await Request(fromDay, toDay);

        Assert.False(result.Succeeded);
        Assert.Contains(expectedError, result.Error);
    }

    [Fact]
    public async Task Request_rejects_own_item()
    {
        var result = await Request(1, 2, renter: _db.Owner.Id);
        Assert.False(result.Succeeded);
        Assert.Contains("own item", result.Error);
    }

    [Fact]
    public async Task Request_rejects_paused_item()
    {
        _db.Item.IsAvailable = false;
        await _db.Context.SaveChangesAsync();

        var result = await Request(1, 2);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Pending_requests_may_overlap_but_accepted_ones_block()
    {
        var first = await Request(1, 3);
        var second = await Request(2, 4, renter: _db.Other.Id);
        Assert.True(first.Succeeded && second.Succeeded, "pending requests don't block each other");

        Assert.True((await _service.AcceptAsync(first.Value!.Id, _db.Owner.Id)).Succeeded);

        var third = await Request(3, 5, renter: _db.Other.Id);
        Assert.False(third.Succeeded);
        Assert.Contains("already booked", third.Error);

        var adjacent = await Request(4, 5, renter: _db.Other.Id);
        Assert.True(adjacent.Succeeded, "the day after an accepted booking is free");
    }

    [Fact]
    public async Task Duplicate_pending_request_from_same_renter_is_rejected()
    {
        await Request(1, 3);
        var again = await Request(2, 3);
        Assert.False(again.Succeeded);
        Assert.Contains("pending request", again.Error);
    }

    [Fact]
    public async Task Accept_declines_overlapping_pending_requests_only()
    {
        var a = (await Request(1, 3)).Value!;
        var overlapping = (await Request(3, 4, renter: _db.Other.Id)).Value!;
        var separate = (await Request(10, 11, renter: _db.Other.Id)).Value!;

        var result = await _service.AcceptAsync(a.Id, _db.Owner.Id);

        Assert.True(result.Succeeded, result.Error);
        await using var fresh = _db.NewContext();
        Assert.Equal(BookingStatus.Accepted, (await fresh.Bookings.FindAsync(a.Id))!.Status);
        Assert.Equal(BookingStatus.Declined, (await fresh.Bookings.FindAsync(overlapping.Id))!.Status);
        Assert.Equal(BookingStatus.Pending, (await fresh.Bookings.FindAsync(separate.Id))!.Status);
    }

    [Fact]
    public async Task Only_the_owner_can_accept_or_decline()
    {
        var booking = (await Request(1, 2)).Value!;

        Assert.False((await _service.AcceptAsync(booking.Id, _db.Renter.Id)).Succeeded);
        Assert.False((await _service.DeclineAsync(booking.Id, _db.Other.Id)).Succeeded);
        Assert.True((await _service.DeclineAsync(booking.Id, _db.Owner.Id)).Succeeded);
        Assert.False((await _service.AcceptAsync(booking.Id, _db.Owner.Id)).Succeeded, "declined can't be accepted");
    }

    [Fact]
    public async Task Renter_can_cancel_until_start_but_not_after()
    {
        var booking = (await Request(1, 3)).Value!;
        await _service.AcceptAsync(booking.Id, _db.Owner.Id);

        Assert.False((await _service.CancelAsync(booking.Id, _db.Other.Id)).Succeeded, "only the renter can cancel");

        _clock.Now = _clock.Now.AddDays(1); // the rental starts today
        var late = await _service.CancelAsync(booking.Id, _db.Renter.Id);
        Assert.False(late.Succeeded);
        Assert.Contains("already started", late.Error);
    }

    [Fact]
    public async Task Cancelled_booking_frees_the_dates()
    {
        var booking = (await Request(1, 3)).Value!;
        await _service.AcceptAsync(booking.Id, _db.Owner.Id);
        Assert.True((await _service.CancelAsync(booking.Id, _db.Renter.Id)).Succeeded);

        Assert.True((await Request(1, 3, renter: _db.Other.Id)).Succeeded);
    }

    [Fact]
    public async Task Complete_requires_started_accepted_booking()
    {
        var booking = (await Request(1, 2)).Value!;
        Assert.False((await _service.CompleteAsync(booking.Id, _db.Owner.Id)).Succeeded, "pending can't complete");

        await _service.AcceptAsync(booking.Id, _db.Owner.Id);
        Assert.False((await _service.CompleteAsync(booking.Id, _db.Owner.Id)).Succeeded, "not started yet");

        _clock.Now = _clock.Now.AddDays(2);
        Assert.True((await _service.CompleteAsync(booking.Id, _db.Owner.Id)).Succeeded);
    }

    [Fact]
    public async Task Review_allowed_once_after_rental_ends()
    {
        var booking = (await Request(1, 2)).Value!;
        await _service.AcceptAsync(booking.Id, _db.Owner.Id);

        Assert.False((await _service.AddReviewAsync(booking.Id, _db.Renter.Id, 5, "Great")).Succeeded, "too early");

        _clock.Now = _clock.Now.AddDays(3); // rental ended yesterday
        Assert.False((await _service.AddReviewAsync(booking.Id, _db.Renter.Id, 6, null)).Succeeded, "rating out of range");
        Assert.False((await _service.AddReviewAsync(booking.Id, _db.Other.Id, 5, null)).Succeeded, "not the renter");
        Assert.True((await _service.AddReviewAsync(booking.Id, _db.Renter.Id, 5, "  Great drill  ")).Succeeded);
        Assert.False((await _service.AddReviewAsync(booking.Id, _db.Renter.Id, 4, null)).Succeeded, "only one review");

        var review = await _db.Context.Reviews.SingleAsync();
        Assert.Equal("Great drill", review.Comment);
        Assert.Equal(_db.Item.Id, review.ItemId);
    }

    [Fact]
    public async Task Blocked_ranges_list_only_upcoming_accepted_bookings()
    {
        var accepted = (await Request(5, 6)).Value!;
        await _service.AcceptAsync(accepted.Id, _db.Owner.Id);
        await Request(8, 9, renter: _db.Other.Id); // pending: not blocked

        var ranges = await _service.GetBlockedRangesAsync(_db.Item.Id);

        Assert.Equal([(Today.AddDays(5), Today.AddDays(6))], ranges);
    }
}
