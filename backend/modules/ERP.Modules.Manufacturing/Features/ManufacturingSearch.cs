using ERP.Application.Abstractions;
using ERP.Application.Search;
using ERP.Modules.Manufacturing.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Manufacturing.Features;

public sealed class ManufacturingSearch(
    IReadRepository<BillOfMaterials> boms,
    IReadRepository<WorkOrder> workOrders,
    ICurrentUser user) : ISearchProvider
{
    public string? ModuleKey => ManufacturingModule.Key;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string term, int limit, CancellationToken ct)
    {
        var hits = new List<SearchHit>();

        if (user.HasPermission(Permissions.BomsRead))
        {
            var bList = await boms.Query(new Specification<BillOfMaterials>(b =>
                b.Name.Contains(term) || b.Code.Contains(term) || b.ProducedItemName.Contains(term)))
                .Take(limit)
                .Select(b => new { b.Id, b.Code, b.Name, b.ProducedItemName })
                .ToListAsync(ct);

            hits.AddRange(bList.Select(b => new SearchHit("Bills of Materials", $"{b.Code} · {b.Name}", b.ProducedItemName, "/manufacturing/boms")));
        }

        if (user.HasPermission(Permissions.WorkOrdersRead) && hits.Count < limit)
        {
            var remaining = limit - hits.Count;
            var wList = await workOrders.Query(new Specification<WorkOrder>(w =>
                w.WorkOrderNumber.Contains(term) || w.ProducedItemName.Contains(term)))
                .Take(remaining)
                .Select(w => new { w.Id, w.WorkOrderNumber, w.ProducedItemName, w.Status })
                .ToListAsync(ct);

            hits.AddRange(wList.Select(w => new SearchHit("Work Orders", $"{w.WorkOrderNumber} · {w.ProducedItemName}", w.Status.ToString(), "/manufacturing/workorders")));
        }

        return hits;
    }
}
