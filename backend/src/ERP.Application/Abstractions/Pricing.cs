namespace ERP.Application.Abstractions;

/// <summary>Asks what an order should charge. Quantities are per item across the whole order, so a quantity break sees the full amount.</summary>
public sealed record PriceQuery(Guid LegalEntityId, DateOnly Date, Guid? CustomerPartyId, IReadOnlyDictionary<Guid, decimal> Quantities);

/// <summary>Asks whether a discount code can be used on an order. Net is the amount before tax, which is what the discount comes off; Total includes tax and is what a minimum order is measured against.</summary>
public sealed record DiscountCodeQuery(Guid LegalEntityId, DateOnly Date, string Code, decimal NetAmount, decimal TotalAmount);

/// <summary>A discount code that applies: how much comes off the net amount of the order.</summary>
public sealed record DiscountCodeResult(Guid DiscountId, string Code, string Name, decimal Amount);

/// <summary>
/// Prices and discount codes for a document, supplied by whichever module holds them. Callers take this as an optional dependency
/// (<c>IEnumerable&lt;IPriceResolver&gt;</c>), so they work with the catalogue price when nothing implements it.
/// </summary>
public interface IPriceResolver
{
    /// <summary>The unit price per item, only for items that have one here. Everything else keeps its catalogue price.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetPricesAsync(PriceQuery query, CancellationToken ct = default);

    /// <summary>Checks a code against an order without using it up. Throws a field error on <c>promoCode</c> when it cannot be used.</summary>
    Task<DiscountCodeResult> QuoteDiscountAsync(DiscountCodeQuery query, CancellationToken ct = default);

    /// <summary>Counts one use, inside the caller's transaction. Throws a conflict when the code has no uses left.</summary>
    Task RedeemDiscountAsync(Guid discountId, CancellationToken ct = default);

    /// <summary>Gives one use back when the document that took it is voided.</summary>
    Task ReleaseDiscountAsync(Guid discountId, CancellationToken ct = default);
}
