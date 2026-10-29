using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Retail.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Retail.Features;

public record RetailKpiSummaryDto(
    int TotalStoresCount,
    int ActiveStoresCount,
    int ActivePriceListsCount,
    int ActivePromotionsCount,
    int TotalPromotionsRedeemed);

public record GetRetailKpiSummaryQuery : IQuery<RetailKpiSummaryDto>;

public class RetailKpiHandlers : IRequestHandler<GetRetailKpiSummaryQuery, RetailKpiSummaryDto>
{
    private readonly IRepository<RetailStore> _stores;
    private readonly IRepository<RetailPriceList> _priceLists;
    private readonly IRepository<RetailPromotion> _promotions;

    public RetailKpiHandlers(
        IRepository<RetailStore> stores,
        IRepository<RetailPriceList> priceLists,
        IRepository<RetailPromotion> promotions)
    {
        _stores = stores;
        _priceLists = priceLists;
        _promotions = promotions;
    }

    public async Task<RetailKpiSummaryDto> Handle(GetRetailKpiSummaryQuery request, CancellationToken cancellationToken)
    {
        var totalStores = await _stores.CountAsync(null, cancellationToken);
        var activeStores = await _stores.CountAsync(s => s.IsActive, cancellationToken);
        var activePriceLists = await _priceLists.CountAsync(p => p.IsActive, cancellationToken);
        var activePromotions = await _promotions.CountAsync(p => p.IsActive, cancellationToken);

        var totalRedeemed = await _promotions.Query().SumAsync(p => (int?)p.TimesUsed, cancellationToken) ?? 0;

        return new RetailKpiSummaryDto(
            totalStores,
            activeStores,
            activePriceLists,
            activePromotions,
            totalRedeemed);
    }
}
