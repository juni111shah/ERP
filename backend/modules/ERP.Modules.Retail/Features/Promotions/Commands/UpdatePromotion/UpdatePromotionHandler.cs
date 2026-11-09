using ERP.Application.Common;
using ERP.Modules.Retail.Repositories;

namespace ERP.Modules.Retail.Features;

internal sealed class UpdatePromotionHandler(IRetailPromotionRepository promotions) : ICommandHandler<UpdatePromotionCommand, RetailPromotionDto>
{
    public async Task<RetailPromotionDto> Handle(UpdatePromotionCommand request, CancellationToken cancellationToken)
    {
        // 1. Load it and apply the changes.
        var promo = await promotions.GetForUpdateAsync(request.Id, cancellationToken);

        promo.Name = request.Name.Trim();
        promo.Type = request.Type;
        promo.DiscountValue = request.DiscountValue;
        promo.MinimumOrderAmount = request.MinimumOrderAmount;
        promo.StartDate = request.StartDate;
        promo.EndDate = request.EndDate;
        promo.UsageLimit = request.UsageLimit;
        promo.IsActive = request.IsActive;
        promo.Description = request.Description?.Trim();
        promo.CustomFields = request.CustomFields;

        // 2. Save.
        await promotions.SaveChangesAsync(cancellationToken);
        return RetailPromotionDto.From(promo);
    }
}
