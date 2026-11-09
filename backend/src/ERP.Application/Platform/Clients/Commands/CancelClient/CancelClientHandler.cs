using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

internal sealed class CancelClientHandler(ClientSubscriptions clients, PlatformLog log, IClock clock) : ICommandHandler<CancelClientCommand, ClientDetail>
{
    public async Task<ClientDetail> Handle(CancelClientCommand command, CancellationToken ct)
    {
        var (org, s) = await clients.LoadAsync(command.OrgId, ct);
        s.Status = SubscriptionStatus.Cancelled;
        s.CancelledAt = clock.UtcNow;
        s.CancelReason = command.Reason?.Trim();
        log.Add("Cancelled", command.OrgId, org.Name, s.CancelReason);
        return await clients.SavedAsync(command.OrgId, ct);
    }
}
