using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class DeclinePlanRequestHandler(ClientSubscriptions clients, PlatformLog log) : ICommandHandler<DeclinePlanRequestCommand, ClientDetail>
{
    public async Task<ClientDetail> Handle(DeclinePlanRequestCommand command, CancellationToken ct)
    {
        var (org, s) = await clients.LoadAsync(command.OrgId, ct);
        s.RequestedPlanId = null; s.RequestedCycle = null; s.RequestNote = null; s.RequestedAt = null;
        log.Add("Plan change declined", command.OrgId, org.Name);
        return await clients.SavedAsync(command.OrgId, ct);
    }
}
