using System.Linq.Expressions;
using ERP.Domain.Platform;

namespace ERP.Application.Reminders;

public sealed record MessageLogDto(Guid Id, string Channel, string Recipient, string Subject, string? Body, string? Link, MessageStatus Status, string? Error, DateTime SentAt, int Attempts = 0)
{
    /// <summary>The message-log list projection (translated to SQL).</summary>
    public static readonly Expression<Func<MessageLog, MessageLogDto>> Projection = m => new MessageLogDto(m.Id, m.Channel, m.Recipient, m.Subject, m.Body, m.Link, m.Status, m.Error, m.SentAt, m.Attempts);
}
