using System.Net;
using System.Net.Mail;
using ERP.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Services;

/// <summary>SMTP implementation of <see cref="IEmailSender"/> (System.Net.Mail). Enabled only when Email:Enabled is true and a host and from-address are set.</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private EmailOptions O => options.Value;
    public bool IsEnabled => O.Enabled && !string.IsNullOrWhiteSpace(O.Host) && !string.IsNullOrWhiteSpace(O.FromAddress);

    public async Task SendAsync(string to, string subject, string bodyText, string? bodyHtml = null, CancellationToken ct = default)
    {
        if (!IsEnabled) throw new InvalidOperationException("Email is not configured.");
        using var message = new MailMessage { From = new MailAddress(O.FromAddress, O.FromName), Subject = subject, Body = bodyHtml ?? bodyText, IsBodyHtml = bodyHtml is not null };
        message.To.Add(new MailAddress(to));
        if (bodyHtml is not null) message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(bodyText, null, "text/plain"));
        using var client = new SmtpClient(O.Host, O.Port) { EnableSsl = O.UseSsl, Timeout = 15_000 };
        if (!string.IsNullOrWhiteSpace(O.Username)) client.Credentials = new NetworkCredential(O.Username, O.Password);
        await client.SendMailAsync(message, ct);
    }
}
