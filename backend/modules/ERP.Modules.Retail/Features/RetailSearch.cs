using ERP.Application.Abstractions;
using ERP.Application.Search;
using ERP.Modules.Retail.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Retail.Features;

public sealed class RetailSearch(
    IReadRepository<RetailStore> stores,
    IReadRepository<RetailPriceList> priceLists,
    IReadRepository<RetailPromotion> promotions,
    ICurrentUser user) : ISearchProvider
{
    public string? ModuleKey => RetailModule.Key;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string term, int limit, CancellationToken ct)
    {
        var hits = new List<SearchHit>();

        if (user.HasPermission(Permissions.StoresRead))
        {
            var sList = await stores.Query(new Specification<RetailStore>(s =>
                s.Name.Contains(term) || s.Code.Contains(term)))
                .Take(limit)
                .Select(s => new { s.Id, s.Code, s.Name, s.IsActive })
                .ToListAsync(ct);

            hits.AddRange(sList.Select(s => new SearchHit("Retail Stores", $"{s.Code} · {s.Name}", s.IsActive ? "Active" : "Inactive", $"/retail/stores")));
        }

        if (user.HasPermission(Permissions.PriceListsRead) && hits.Count < limit)
        {
            var remaining = limit - hits.Count;
            var pList = await priceLists.Query(new Specification<RetailPriceList>(p =>
                p.Name.Contains(term) || p.Code.Contains(term)))
                .Take(remaining)
                .Select(p => new { p.Id, p.Code, p.Name, p.Currency })
                .ToListAsync(ct);

            hits.AddRange(pList.Select(p => new SearchHit("Retail Price Lists", $"{p.Code} · {p.Name}", p.Currency, $"/retail/pricelists")));
        }

        if (user.HasPermission(Permissions.PromotionsRead) && hits.Count < limit)
        {
            var remaining = limit - hits.Count;
            var promoList = await promotions.Query(new Specification<RetailPromotion>(p =>
                p.Name.Contains(term) || p.Code.Contains(term)))
                .Take(remaining)
                .Select(p => new { p.Id, p.Code, p.Name, p.DiscountValue })
                .ToListAsync(ct);

            hits.AddRange(promoList.Select(p => new SearchHit("Retail Promotions", $"{p.Code} · {p.Name}", $"Discount: {p.DiscountValue}", $"/retail/promotions")));
        }

        return hits;
    }
}
