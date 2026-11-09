using System.Net.Mail;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Printing;

/// <summary>Shared by the document email handlers: checks email is set up, resolves the recipient, sends and records the message.</summary>
public sealed class DocumentEmailSender(IEmailSender email, IRepository<MessageLog> log, ICurrentUser user, IClock clock)
{
    public void EnsureConfigured()
    {
        if (!email.IsEnabled) throw new ConflictException("Email is not configured for this system. Ask an administrator to set the Email settings.");
    }

    public void Require(string permission)
    {
        if (!user.HasPermission(permission)) throw new ForbiddenException();
    }

    public static string ResolveRecipient(string? requested, string? partyEmail)
    {
        var to = string.IsNullOrWhiteSpace(requested) ? partyEmail : requested;
        if (string.IsNullOrWhiteSpace(to)) throw new FieldValidationException("to", "This party has no email address. Enter one to send to.");
        try { _ = new MailAddress(to.Trim()); } catch (FormatException) { throw new FieldValidationException("to", "Enter a valid email address."); }
        return to.Trim();
    }

    internal async Task<EmailSentDto> SendAsync(string kind, string id, EmailDoc doc, string to, CancellationToken ct)
    {
        var subject = doc.Title + (string.IsNullOrWhiteSpace(doc.Company) ? "" : $" - {doc.Company}");
        var text = DocumentEmailRenderer.Text(doc);
        var entry = new MessageLog
        {
            Channel = "email", Recipient = to, Subject = subject.Length > 400 ? subject[..400] : subject, Body = text.Length > 2000 ? text[..2000] : text,
            DedupeKey = $"doc-email:{kind}:{id}:{Guid.NewGuid():N}", SentAt = clock.UtcNow, Attempts = 1, Status = MessageStatus.Sent,
        };
        try { await email.SendAsync(to, subject, text, DocumentEmailRenderer.Html(doc), ct); }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            entry.Status = MessageStatus.Failed; entry.Error = e.Message.Length > 400 ? e.Message[..400] : e.Message;
            log.Add(entry);
            await log.SaveChangesAsync(ct);
            throw new ConflictException($"The email could not be sent: {e.Message}");
        }
        log.Add(entry);
        await log.SaveChangesAsync(ct);
        return new(to, subject);
    }
}
