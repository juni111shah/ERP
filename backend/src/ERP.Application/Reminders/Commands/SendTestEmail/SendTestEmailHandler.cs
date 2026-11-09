using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Reminders;

internal sealed class SendTestEmailHandler(IEmailSender sender, IRepository<MessageLog> log, IClock clock) : ICommandHandler<SendTestEmailCommand, MessageLogDto>
{
    public async Task<MessageLogDto> Handle(SendTestEmailCommand command, CancellationToken ct)
    {
        // 1. Email must be configured.
        if (!sender.IsEnabled) throw new ConflictException("Email is not configured. Set the Email section (Enabled, Host, FromAddress ...) and restart the API.");

        // 2. Send; a failure is recorded, not thrown.
        var entry = new MessageLog { Channel = "email", Recipient = command.To.Trim(), Subject = "ERP test email", Body = "This is a test message from your ERP. Email sending works.", DedupeKey = $"test-email:{Guid.NewGuid()}", SentAt = clock.UtcNow, Attempts = 1, Status = MessageStatus.Sent };
        try { await sender.SendAsync(entry.Recipient, entry.Subject, entry.Body, null, ct); }
        catch (Exception e) when (e is not OperationCanceledException) { entry.Status = MessageStatus.Failed; entry.Error = e.Message.Length > 400 ? e.Message[..400] : e.Message; }

        // 3. Log it.
        log.Add(entry);
        await log.SaveChangesAsync(ct);
        return new(entry.Id, entry.Channel, entry.Recipient, entry.Subject, entry.Body, null, entry.Status, entry.Error, entry.SentAt, entry.Attempts);
    }
}
