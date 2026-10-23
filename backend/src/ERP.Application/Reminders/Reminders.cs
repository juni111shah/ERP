using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Identity;
using ERP.Application.Security;
using ERP.Domain.Platform;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Reminders;

/// <summary>
/// One thing someone should be reminded about. DedupeKey identifies it (e.g. "bill-due:{id}:2026-11-01") so it is sent once per recipient.
/// Audience: internal users holding <see cref="Permission"/> (in-app) and/or an external <see cref="Email"/> (tenant, vendor).
/// </summary>
public sealed record ReminderCandidate(string DedupeKey, string Subject, string? Body, string? Link, string? Permission, string? Email = null, string Severity = "warning");

/// <summary>Modules and core contribute reminders by implementing this; the dispatcher job does the sending.</summary>
public interface IReminderSource
{
    string? ModuleKey { get; }
    Task<IReadOnlyList<ReminderCandidate>> GetDueAsync(DateOnly today, CancellationToken ct);
}

public sealed record OutboundMessage(string Recipient, string Subject, string? Body, string? Link, string Severity);

/// <summary>Delivery channel (in-app, email, SMS, WhatsApp...). New channels are registered in DI; the dispatcher uses all of them for the targets they support.</summary>
public interface IMessageChannel
{
    string Name { get; }
    Task SendAsync(OutboundMessage message, CancellationToken ct);
}

/// <summary>In-app channel: the recipient is a user id.</summary>
public sealed class InAppChannel(INotifier notifier) : IMessageChannel
{
    public string Name => "inapp";
    public Task SendAsync(OutboundMessage m, CancellationToken ct) => notifier.NotifyUserAsync(Guid.Parse(m.Recipient), m.Subject, m.Body, m.Link, m.Severity, ct);
}

/// <summary>Default email channel: nothing is sent until a real sender (SMTP, provider API) replaces it in DI; the message is still recorded in the communication log.</summary>
public sealed class OutboxEmailChannel : IMessageChannel
{
    public string Name => "email";
    public Task SendAsync(OutboundMessage message, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Collects the due reminders of every enabled source and sends each once per recipient and channel.</summary>
public sealed class ReminderDispatcher(IEnumerable<IReminderSource> sources, IEnumerable<IMessageChannel> channels, IModuleAccess modules,
    IRepository<MessageLog> log, IReadRepository<UserRole> userRoles, IClock clock)
{
    public async Task<int> RunAsync(DateOnly today, CancellationToken ct)
    {
        var sent = 0;
        var inapp = channels.Single(c => c.Name == "inapp");
        var email = channels.Single(c => c.Name == "email");
        foreach (var source in sources)
        {
            if (source.ModuleKey is { } key && !await modules.IsEnabledAsync(key, ct)) continue;
            foreach (var c in await source.GetDueAsync(today, ct))
            {
                if (c.Permission is not null)
                    foreach (var userId in await UsersWithAsync(c.Permission, ct))
                        sent += await SendOnceAsync(inapp, userId.ToString(), c, ct);
                if (!string.IsNullOrWhiteSpace(c.Email)) sent += await SendOnceAsync(email, c.Email!, c, ct);
            }
        }
        return sent;
    }

    private async Task<int> SendOnceAsync(IMessageChannel channel, string recipient, ReminderCandidate c, CancellationToken ct)
    {
        var key = $"{c.DedupeKey}|{channel.Name}|{recipient}";
        if (await log.AnyAsync(x => x.DedupeKey == key, ct)) return 0;
        var entry = new MessageLog { Channel = channel.Name, Recipient = recipient, Subject = c.Subject, Body = c.Body, Link = c.Link, DedupeKey = key, SentAt = clock.UtcNow, Status = MessageStatus.Sent };
        try { await channel.SendAsync(new(recipient, c.Subject, c.Body, c.Link, c.Severity), ct); }
        catch (Exception e) when (e is not OperationCanceledException) { entry.Status = MessageStatus.Failed; entry.Error = e.Message; }
        log.Add(entry);
        await log.SaveChangesAsync(ct);
        return entry.Status == MessageStatus.Sent ? 1 : 0;
    }

    private async Task<List<Guid>> UsersWithAsync(string permission, CancellationToken ct)
        => (await userRoles.Query(new Specification<UserRole>(ur => ur.Role!.Permissions.Any(p => p.Permission == permission || p.Permission == "*"))).Select(ur => ur.UserId).Distinct().ToListAsync(ct));
}

/// <summary>Nightly job: send whatever reminders became due.</summary>
public sealed class ReminderJob(ReminderDispatcher dispatcher) : IScheduledJob
{
    public string Name => "core.reminders";
    public string? ModuleKey => null;
    public Task RunAsync(DateOnly today, CancellationToken ct) => dispatcher.RunAsync(today, ct);
}

public sealed record MessageLogDto(Guid Id, string Channel, string Recipient, string Subject, string? Body, string? Link, MessageStatus Status, string? Error, DateTime SentAt);

/// <summary>Runs the reminder pass now (admins and tests); returns how many messages were sent.</summary>
public sealed record SendRemindersNow : ICommand<int>, IRequirePermission { public string RequiredPermission => ERP.Application.Security.CorePermissions.SettingsWrite; }

internal sealed class ReminderHandlers(ReminderDispatcher dispatcher, IClock clock) : IRequestHandler<SendRemindersNow, int>
{
    public Task<int> Handle(SendRemindersNow cmd, CancellationToken ct) => dispatcher.RunAsync(DateOnly.FromDateTime(clock.UtcNow), ct);
}
