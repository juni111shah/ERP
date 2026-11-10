using ERP.Application.Common;
using ERP.Modules.Distribution.Repositories;

namespace ERP.Modules.Distribution.Features;

internal sealed class CalculateWholesaleRateHandler(IWholesalePriceMatrixRepository matrix) : IQueryHandler<CalculateWholesaleRateQuery, WholesaleRateCalculationDto>
{
    public async Task<WholesaleRateCalculationDto> Handle(CalculateWholesaleRateQuery request, CancellationToken cancellationToken)
    {
        // 1. The rule whose quantity band holds the quantity.
        var matchingRule = await matrix.FindMatchingRuleAsync(request.LegalEntityId, request.ItemId, request.Tier, request.Quantity, cancellationToken);

        // 2. Price it, or answer that no tier applies.
        if (matchingRule != null)
        {
            var total = Math.Round(matchingRule.WholesaleUnitPrice * request.Quantity, 2);
            return new WholesaleRateCalculationDto(
                request.ItemId,
                request.Tier,
                request.Quantity,
                matchingRule.WholesaleUnitPrice,
                matchingRule.DiscountPercentage,
                total,
                true);
        }

        return new WholesaleRateCalculationDto(
            request.ItemId,
            request.Tier,
            request.Quantity,
            0,
            0,
            0,
            false);
    }
}
