using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Retail.Domain;
using ERP.Modules.Retail.Repositories;

namespace ERP.Modules.Retail.Features;

internal sealed class GetRetailKpiSummaryHandler(
    IReadRepository<RetailStore> stores,
    IReadRepository<RetailPriceList> priceLists,
    IRetailPromotionRepository promotions)
    : IQueryHandler<GetRetailKpiSummaryQuery, RetailKpiSummaryDto>
{
    public async Task<RetailKpiSummaryDto> Handle(GetRetailKpiSummaryQuery request, CancellationToken cancellationToken)
    {
        // 1. Counts.
        var totalStores = await stores.CountAsync(null, cancellationToken);
        var activeStores = await stores.CountAsync(s => s.IsActive, cancellationToken);
        var activePriceLists = await priceLists.CountAsync(p => p.IsActive, cancellationToken);
        var activePromotions = await promotions.CountAsync(p => p.IsActive, cancellationToken);

        // 2. Redemptions.
        var totalRedeemed = await promotions.TotalTimesUsedAsync(cancellationToken);

        return new RetailKpiSummaryDto(
            totalStores,
            activeStores,
            activePriceLists,
            activePromotions,
            totalRedeemed);
    }
}
