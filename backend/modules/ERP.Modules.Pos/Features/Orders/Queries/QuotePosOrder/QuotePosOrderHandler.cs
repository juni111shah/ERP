using ERP.Application.Common;

namespace ERP.Modules.Pos.Features;

internal sealed class QuotePosOrderHandler(PosOrderPricing pricing, PosOrderParties people) : IQueryHandler<QuotePosOrderQuery, PosOrderQuoteDto>
{
    public async Task<PosOrderQuoteDto> Handle(QuotePosOrderQuery request, CancellationToken cancellationToken)
    {
        var (customerId, _) = await people.CustomerAsync(request.CustomerPartyId, null, cancellationToken);
        var priced = await pricing.ApplyPromoAsync(request.LegalEntityId, await pricing.PriceAsync(request.LegalEntityId, customerId, request.Lines, cancellationToken), request.PromoCode, cancellationToken);
        return new PosOrderQuoteDto(priced.SubTotal, priced.TaxTotal, priced.DiscountTotal, priced.Total, priced.Promo?.Code, priced.Promo?.Name, priced.Promo?.Amount ?? 0m,
            priced.Lines.Select(l => new PosOrderQuoteLineDto(l.ItemId, l.ItemCode, l.Description, l.Quantity, l.UnitPrice, l.DiscountPercent, l.PromoDiscount, l.TaxRate, l.LineTotal)).ToList());
    }
}
