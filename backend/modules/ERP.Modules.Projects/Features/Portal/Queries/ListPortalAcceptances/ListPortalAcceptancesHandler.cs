using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class ListPortalAcceptancesHandler(IProjectAcceptanceRepository acceptances, IProjectRepository projects, ICurrentUser user)
    : IQueryHandler<ListPortalAcceptancesQuery, IReadOnlyList<PortalAcceptanceDto>>
{
    public async Task<IReadOnlyList<PortalAcceptanceDto>> Handle(ListPortalAcceptancesQuery query, CancellationToken ct)
    {
        var me = PortalCustomer.IdOf(user);
        var mineIds = await projects.ListIdsOfCustomerAsync(me, ct);
        var names = await projects.MapNamesAsync(mineIds, ct);
        var rows = await acceptances.ListOfProjectsAsync(mineIds, ct);
        return rows.OrderBy(a => a.Status).ThenByDescending(a => a.RequestedAt).Select(a => PortalAcceptanceDto.From(a, names.GetValueOrDefault(a.ProjectId, ""))).ToList();
    }
}
