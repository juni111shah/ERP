using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Retail.Repositories;

namespace ERP.Modules.Retail.Features;

internal sealed class ValidatePromoCodeHandler(IRetailPromotionRepository promotions, ILegalEntityGuard guard) : IQueryHandler<ValidatePromoCodeQuery, PromoValidationResultDto>
{
    public async Task<PromoValidationResultDto> Handle(ValidatePromoCodeQuery request, CancellationToken cancellationToken)
    {
        // 1. Find the legal entity's promotion with this code.
        var code = request.PromoCode.Trim().ToUpperInvariant();
        var today = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);

        var promo = await promotions.FindByCodeAsync(code, request.LegalEntityId, cancellationToken);

        // 2. Check it can be used, then work out the discount.
        var currency = (await guard.RequireAsync(request.LegalEntityId, cancellationToken)).BaseCurrency;
        var problem = PromotionRules.Problem(promo, today, request.OrderTotal, currency);
        if (problem is not null)
        {
            return new PromoValidationResultDto(false, problem, null, 0, 0);
        }

        return new PromoValidationResultDto(true, "Promotion applied successfully.", promo!.Type, promo.DiscountValue, PromotionRules.Discount(promo, request.OrderTotal));
    }
}
