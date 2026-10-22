using ERP.Application.Abstractions;
using ERP.Application.Search;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

/// <summary>Properties, units, leases, deals and acquisitions for the global search.</summary>
public sealed class RealEstateSearch(
    IReadRepository<Property> properties, IReadRepository<Unit> units, IReadRepository<Lease> leases, IReadRepository<BrokerageDeal> deals, IReadRepository<Acquisition> acquisitions, ICurrentUser user) : ISearchProvider
{
    public string? ModuleKey => RealEstateModule.Key;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string term, int limit, CancellationToken ct)
    {
        var hits = new List<SearchHit>();
        if (user.HasPermission(P.PropertiesRead))
            hits.AddRange((await properties.Query(new Specification<Property>(p => p.Name.Contains(term) || p.Code.Contains(term) || (p.Community != null && p.Community.Contains(term)))).Take(limit).Select(p => new { p.Id, p.Name, p.Code }).ToListAsync(ct))
                .Select(p => new SearchHit("Properties", p.Name, p.Code, $"/real-estate/properties/{p.Id}")));
        if (user.HasPermission(P.UnitsRead))
            hits.AddRange((await units.Query(new Specification<Unit>(u => u.UnitNumber.Contains(term))).Take(limit).Select(u => new { u.PropertyId, u.UnitNumber }).ToListAsync(ct))
                .Select(u => new SearchHit("Units", $"Unit {u.UnitNumber}", null, $"/real-estate/properties/{u.PropertyId}")));
        if (user.HasPermission(P.LeasesRead))
            hits.AddRange((await leases.Query(new Specification<Lease>(l => l.Number != null && l.Number.Contains(term))).Take(limit).Select(l => new { l.Id, l.Number }).ToListAsync(ct))
                .Select(l => new SearchHit("Leases", l.Number!, null, $"/real-estate/leases/{l.Id}")));
        if (user.HasPermission(P.DealsRead))
            hits.AddRange((await deals.Query(new Specification<BrokerageDeal>(d => (d.Number != null && d.Number.Contains(term)) || d.ClientName.Contains(term))).Take(limit).Select(d => new { d.Number, d.ClientName }).ToListAsync(ct))
                .Select(d => new SearchHit("Deals", d.Number ?? "Deal", d.ClientName, "/real-estate/deals")));
        if (user.HasPermission(P.AcquisitionsRead))
            hits.AddRange((await acquisitions.Query(new Specification<Acquisition>(a => (a.Number != null && a.Number.Contains(term)) || a.SellerName.Contains(term) || (a.ContractReference != null && a.ContractReference.Contains(term)))).Take(limit).Select(a => new { a.Id, a.Number, a.SellerName }).ToListAsync(ct))
                .Select(a => new SearchHit("Acquisitions", a.Number ?? "Acquisition", a.SellerName, $"/real-estate/acquisitions/{a.Id}")));
        return hits;
    }
}
