using System.Threading.Channels;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace Rentilo.Services;

public record EmailMessage(string To, string Subject, string HtmlBody);

/// <summary>
/// In-memory queue so pages never wait for (or fail because of) the mail server.
/// A background service drains it; failures are logged and dropped.
/// </summary>
public interface IEmailQueue
{
    void Enqueue(EmailMessage message);
}

public sealed class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public void Enqueue(EmailMessage message) => _channel.Writer.TryWrite(message);
}

public sealed class EmailBackgroundSender(EmailQueue queue, IServiceScopeFactory scopes, ILogger<EmailBackgroundSender> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                await sender.SendEmailAsync(message.To, message.Subject, message.HtmlBody);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to send email \"{Subject}\" to {To}", message.Subject, message.To);
            }
        }
    }
}
