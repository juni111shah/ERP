using ERP.Application.Abstractions;
using ERP.Application.Search;
using ERP.Modules.Purchasing.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Features;

public sealed class PurchasingSearch(
    IReadRepository<PurchaseOrder> orders,
    IReadRepository<PurchaseRequisition> requisitions,
    ICurrentUser user) : ISearchProvider
{
    public string? ModuleKey => PurchasingModule.Key;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string term, int limit, CancellationToken ct)
    {
        var hits = new List<SearchHit>();

        if (user.HasPermission(P.OrdersRead))
        {
            var oList = await orders.Query(new Specification<PurchaseOrder>(o =>
                o.Number.Contains(term) || o.VendorName.Contains(term) || (o.VendorReference != null && o.VendorReference.Contains(term))))
                .Take(limit)
                .Select(o => new { o.Id, o.Number, o.VendorName, o.Status, o.TotalAmount, o.Currency })
                .ToListAsync(ct);

            hits.AddRange(oList.Select(o => new SearchHit("Purchase Orders", $"{o.Number} · {o.VendorName}", $"{o.Currency} {o.TotalAmount:N2} · {o.Status}", $"/purchasing/orders/{o.Id}")));
        }

        if (user.HasPermission(P.RequisitionsRead) && hits.Count < limit)
        {
            var remaining = limit - hits.Count;
            var rList = await requisitions.Query(new Specification<PurchaseRequisition>(r =>
                r.Number.Contains(term) || r.RequesterName.Contains(term)))
                .Take(remaining)
                .Select(r => new { r.Id, r.Number, r.RequesterName, r.Status, r.EstimatedTotal })
                .ToListAsync(ct);

            hits.AddRange(rList.Select(r => new SearchHit("Purchase Requisitions", $"{r.Number} · {r.RequesterName}", $"Est: QAR {r.EstimatedTotal:N2} · {r.Status}", $"/purchasing/requisitions")));
        }

        return hits;
    }
}
