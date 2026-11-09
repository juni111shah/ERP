using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class SuspendClientHandler(ClientSubscriptions clients, PlatformLog log, IPlatformMailer mailer, IClock clock) : ICommandHandler<SuspendClientCommand, ClientDetail>
{
    public async Task<ClientDetail> Handle(SuspendClientCommand command, CancellationToken ct)
    {
        var (org, s) = await clients.LoadAsync(command.OrgId, ct);
        if (s.Status == SubscriptionStatus.Cancelled) throw new ConflictException("This client is cancelled.");
        s.Status = SubscriptionStatus.Suspended;
        s.SuspendedAt = clock.UtcNow;
        s.SuspendedReason = command.Reason?.Trim();
        s.SuspendedByFounder = true;
        log.Add("Suspended", command.OrgId, org.Name, s.SuspendedReason);
        var detail = await clients.SavedAsync(command.OrgId, ct);
        await mailer.SendAsync(s.BillingEmail, "Your workspace is suspended", $"{org.Name} has been suspended. Your data is kept.{(s.SuspendedReason is { Length: > 0 } r ? $"\n\nReason: {r}" : "")}", ct);
        return detail;
    }
}
