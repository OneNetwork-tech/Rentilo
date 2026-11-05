using Rentilo.Services;

namespace Rentilo.Tests;

/// <summary>Captures notifications as "kind:id" strings.</summary>
public sealed class RecordingNotifier : INotifier
{
    public List<string> Events { get; } = [];
    public Task BookingRequestedAsync(int bookingId) { Events.Add($"requested:{bookingId}"); return Task.CompletedTask; }
    public Task BookingAcceptedAsync(int bookingId) { Events.Add($"accepted:{bookingId}"); return Task.CompletedTask; }
    public Task BookingDeclinedAsync(int bookingId, bool automatically = false) { Events.Add($"declined{(automatically ? "-auto" : "")}:{bookingId}"); return Task.CompletedTask; }
    public Task BookingCancelledAsync(int bookingId) { Events.Add($"cancelled:{bookingId}"); return Task.CompletedTask; }
    public Task NewMessageAsync(int messageId) { Events.Add($"message:{messageId}"); return Task.CompletedTask; }
}
