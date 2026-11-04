using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace Rentilo.Services;

public class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "no-reply@rentilo.local";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}

/// <summary>
/// Sends mail over SMTP when <c>Email:Smtp:Host</c> is configured; otherwise logs the message
/// (so confirmation and reset links are visible in the console during development).
/// </summary>
public class EmailSender(IOptions<SmtpOptions> options, ILogger<EmailSender> logger) : IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var smtp = options.Value;
        if (!smtp.IsConfigured)
        {
            logger.LogWarning("SMTP not configured. Email to {Email} with subject \"{Subject}\":\n{Body}", email, subject, htmlMessage);
            return;
        }

        using var client = new SmtpClient(smtp.Host, smtp.Port) { EnableSsl = smtp.EnableSsl };
        if (!string.IsNullOrEmpty(smtp.UserName))
            client.Credentials = new NetworkCredential(smtp.UserName, smtp.Password);

        using var message = new MailMessage(smtp.From, email, subject, htmlMessage) { IsBodyHtml = true };
        await client.SendMailAsync(message);
    }
}
