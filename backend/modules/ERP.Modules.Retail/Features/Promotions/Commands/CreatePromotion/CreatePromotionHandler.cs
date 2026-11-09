using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Retail.Domain;
using ERP.Modules.Retail.Repositories;

namespace ERP.Modules.Retail.Features;

internal sealed class CreatePromotionHandler(IRetailPromotionRepository promotions, ILegalEntityGuard guard) : ICommandHandler<CreatePromotionCommand, RetailPromotionDto>
{
    public async Task<RetailPromotionDto> Handle(CreatePromotionCommand request, CancellationToken cancellationToken)
    {
        // 1. Build it, unused.
        await guard.RequireAsync(request.LegalEntityId, cancellationToken);
        var promo = new RetailPromotion
        {
            LegalEntityId = request.LegalEntityId,
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            Type = request.Type,
            DiscountValue = request.DiscountValue,
            MinimumOrderAmount = request.MinimumOrderAmount,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            UsageLimit = request.UsageLimit,
            TimesUsed = 0,
            IsActive = request.IsActive,
            Description = request.Description?.Trim(),
            CustomFields = request.CustomFields
        };

        // 2. Save.
        promotions.Add(promo);
        await promotions.SaveChangesAsync(cancellationToken);
        return RetailPromotionDto.From(promo);
    }
}
