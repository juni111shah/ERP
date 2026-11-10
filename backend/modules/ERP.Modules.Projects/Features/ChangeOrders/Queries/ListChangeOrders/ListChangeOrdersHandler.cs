using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class ListChangeOrdersHandler(IProjectChangeOrderRepository changeOrders, ProjectAccess access) : IQueryHandler<ListChangeOrdersQuery, IReadOnlyList<ChangeOrderDto>>
{
    public async Task<IReadOnlyList<ChangeOrderDto>> Handle(ListChangeOrdersQuery query, CancellationToken ct)
    {
        var visible = await access.VisibleIdsAsync(ct);
        var rows = await changeOrders.ListByProjectAsync(query.ProjectId, ct);
        return rows.Where(c => visible == null || visible.Contains(c.ProjectId)).OrderByDescending(c => c.CreatedAt).Select(ChangeOrderDto.From).ToList();
    }
}
