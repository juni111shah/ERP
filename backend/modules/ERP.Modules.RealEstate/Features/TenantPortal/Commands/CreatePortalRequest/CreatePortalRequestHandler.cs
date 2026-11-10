using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CreatePortalRequestHandler(
    ICurrentUser user, ILeaseRepository leases, IRepository<WorkOrder> workOrders, IReadRepository<ERP.Domain.Parties.Party> parties, INumberingService numbers, IClock clock)
    : ICommandHandler<CreatePortalRequestCommand, PortalRequestDto>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<PortalRequestDto> Handle(CreatePortalRequestCommand cmd, CancellationToken ct)
    {
        // 1. The tenant's own active lease.
        var me = TenantPortalUser.Me(user);
        var i = cmd.Input;
        var lease = await leases.FindOfTenantAsync(i.LeaseId, me, ct) ?? throw new NotFoundException();
        if (lease.Status is not (LeaseStatus.Active or LeaseStatus.HoldingOver)) throw new ConflictException("Requests can only be raised for an active lease.");

        // 2. A numbered work order reported by the tenant; save.
        var name = (await parties.GetAsync(me, ct)).Name;
        var w = new WorkOrder
        {
            LegalEntityId = lease.LegalEntityId, PropertyId = lease.PropertyId, UnitId = lease.UnitId, LeaseId = lease.Id, Issue = i.Issue.Trim(), Category = i.Category, Priority = i.Priority,
            ReportedOn = Today, ReportedBy = $"{name} (tenant portal)", Details = i.Details,
        };
        w.Number = await numbers.NextAsync(Docs.WorkOrder, w.LegalEntityId, Today, ct);
        workOrders.Add(w);
        await workOrders.SaveChangesAsync(ct);
        return PortalRequestDto.From(w);
    }
}
