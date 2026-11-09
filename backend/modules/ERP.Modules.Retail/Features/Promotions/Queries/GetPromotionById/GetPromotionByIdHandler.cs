using ERP.Application.Common;
using ERP.Modules.Retail.Repositories;

namespace ERP.Modules.Retail.Features;

internal sealed class GetPromotionByIdHandler(IRetailPromotionRepository promotions) : IQueryHandler<GetPromotionByIdQuery, RetailPromotionDto?>
{
    public async Task<RetailPromotionDto?> Handle(GetPromotionByIdQuery request, CancellationToken cancellationToken)
    {
        var promo = await promotions.FindAsync(request.Id, cancellationToken);
        return promo is null ? null : RetailPromotionDto.From(promo);
    }
}
