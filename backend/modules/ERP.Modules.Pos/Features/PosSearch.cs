using ERP.Application.Abstractions;
using ERP.Application.Search;
using ERP.Modules.Pos.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Features;

public sealed class PosSearch(
    IReadRepository<PosOrder> orders,
    IReadRepository<PosRegister> registers,
    ICurrentUser user) : ISearchProvider
{
    public string? ModuleKey => PosModule.Key;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string term, int limit, CancellationToken ct)
    {
        var hits = new List<SearchHit>();

        if (user.HasPermission(Permissions.OrdersRead))
        {
            var oList = await orders.Query(new Specification<PosOrder>(o =>
                o.Number.Contains(term) || o.CustomerName.Contains(term) || o.CashierName.Contains(term)))
                .Take(limit)
                .Select(o => new { o.Id, o.Number, o.CustomerName, o.Status, o.TotalAmount })
                .ToListAsync(ct);

            hits.AddRange(oList.Select(o => new SearchHit("POS Orders", $"{o.Number} · {o.CustomerName}", $"AED {o.TotalAmount:N2} · {o.Status}", $"/pos/orders/{o.Id}")));
        }

        if (user.HasPermission(Permissions.RegistersRead) && hits.Count < limit)
        {
            var remaining = limit - hits.Count;
            var rList = await registers.Query(new Specification<PosRegister>(r =>
                r.Name.Contains(term) || r.Code.Contains(term)))
                .Take(remaining)
                .Select(r => new { r.Id, r.Code, r.Name, r.IsActive })
                .ToListAsync(ct);

            hits.AddRange(rList.Select(r => new SearchHit("POS Registers", $"{r.Code} · {r.Name}", r.IsActive ? "Active" : "Inactive", $"/pos/registers")));
        }

        return hits;
    }
}
