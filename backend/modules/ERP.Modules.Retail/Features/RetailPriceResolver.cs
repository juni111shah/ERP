using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Catalog;
using ERP.Modules.Retail.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Retail.Features;

/// <summary>When a promotion can be used and what it takes off. Shared by the code check screen and the till.</summary>
public static class PromotionRules
{
    /// <summary>Why this promotion cannot be used on an order of this total today, or null when it can.</summary>
    public static string? Problem(RetailPromotion? promo, DateOnly today, decimal orderTotal, string currency)
    {
        if (promo is null || !promo.IsActive) return "Invalid or inactive promotion code.";
        if (today < promo.StartDate || today > promo.EndDate) return "Promotion code has expired or is not yet active.";
        if (promo.UsageLimit.HasValue && promo.TimesUsed >= promo.UsageLimit.Value) return "Promotion usage limit has been reached.";
        if (promo.MinimumOrderAmount.HasValue && orderTotal < promo.MinimumOrderAmount.Value) return $"Order must be at least {currency} {promo.MinimumOrderAmount:N2} to use this coupon.";
        // These two carry no item scope to work out a discount from, so they must not read as "valid, nothing off" and burn a use.
        if (promo.Type is PromotionType.BuyXGetY or PromotionType.Bundle) return "This type of promotion cannot be applied to an order yet.";
        return null;
    }

    public static decimal Discount(RetailPromotion promo, decimal amount) => promo.Type switch
    {
        PromotionType.PercentageDiscount => Math.Round(amount * (promo.DiscountValue / 100m), 2),
        PromotionType.FixedAmountDiscount => Math.Min(amount, promo.DiscountValue),
        _ => 0m,
    };
}

/// <summary>
/// Retail's prices and codes for whoever sells. Price lists of the legal entity's base currency apply when active and in date:
/// a Standard list only if it is the default, Promotional and Tiered always, Member only with a customer on the document. The lowest price among them wins.
/// </summary>
public sealed class RetailPriceResolver(
    IModuleAccess moduleAccess,
    IReadRepository<RetailPriceList> priceLists,
    IRepository<RetailPromotion> promotions,
    IReadRepository<Item> items,
    ILegalEntityGuard guard) : IPriceResolver
{
    public async Task<IReadOnlyDictionary<Guid, decimal>> GetPricesAsync(PriceQuery query, CancellationToken ct = default)
    {
        var prices = new Dictionary<Guid, decimal>();
        if (query.Quantities.Count == 0 || !await moduleAccess.IsEnabledAsync(RetailModule.Key, ct)) return prices;

        var entity = query.LegalEntityId;
        var date = query.Date;
        var lists = await priceLists.Query(new Specification<RetailPriceList>(p => p.LegalEntityId == entity && p.IsActive
                && (p.ValidFrom == null || p.ValidFrom <= date) && (p.ValidTo == null || p.ValidTo >= date)))
            .Include(p => p.Items).ToListAsync(ct);
        if (lists.Count == 0) return prices;

        var currency = (await guard.RequireAsync(entity, ct)).BaseCurrency;
        var applicable = lists.Where(p => string.Equals(p.Currency, currency, StringComparison.OrdinalIgnoreCase) && p.Type switch
        {
            PriceListType.Standard => p.IsDefault,
            PriceListType.Member => query.CustomerPartyId.HasValue,
            _ => true,
        }).ToList();
        if (applicable.Count == 0) return prices;

        // A list row may name its item by id or only by code.
        var ids = query.Quantities.Keys.ToList();
        var skus = await items.Query(new Specification<Item>(i => ids.Contains(i.Id))).Select(i => new { i.Id, i.Sku }).ToDictionaryAsync(i => i.Id, i => i.Sku, ct);

        foreach (var (itemId, quantity) in query.Quantities)
        {
            skus.TryGetValue(itemId, out var sku);
            var candidates = applicable.SelectMany(p => p.Items)
                .Where(row => (row.ItemId == itemId || (row.ItemId is null && row.ItemCode == sku)) && (row.MinQuantity ?? 0m) <= quantity)
                .Select(row => (decimal?)row.Price).ToList();
            if (candidates.Count > 0) prices[itemId] = candidates.Min()!.Value;
        }
        return prices;
    }

    public async Task<DiscountCodeResult> QuoteDiscountAsync(DiscountCodeQuery query, CancellationToken ct = default)
    {
        if (!await moduleAccess.IsEnabledAsync(RetailModule.Key, ct)) throw new FieldValidationException("promoCode", "Discount codes are not available here.");

        var code = query.Code.Trim().ToUpperInvariant();
        var entity = query.LegalEntityId;
        var promo = await promotions.Query(new Specification<RetailPromotion>(p => p.Code == code && p.LegalEntityId == entity)).FirstOrDefaultAsync(ct);
        var currency = (await guard.RequireAsync(entity, ct)).BaseCurrency;
        var problem = PromotionRules.Problem(promo, query.Date, query.TotalAmount, currency);
        if (problem is not null) throw new FieldValidationException("promoCode", problem);
        return new DiscountCodeResult(promo!.Id, promo.Code, promo.Name, PromotionRules.Discount(promo, query.NetAmount));
    }

    public async Task RedeemDiscountAsync(Guid discountId, CancellationToken ct = default)
    {
        var updated = await promotions.Tracked(new Specification<RetailPromotion>(p => p.Id == discountId && (p.UsageLimit == null || p.TimesUsed < p.UsageLimit)))
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.TimesUsed, p => p.TimesUsed + 1), ct);
        if (updated == 0) throw new ConflictException("Promotion usage limit has been reached.");
    }

    public async Task ReleaseDiscountAsync(Guid discountId, CancellationToken ct = default)
        => await promotions.Tracked(new Specification<RetailPromotion>(p => p.Id == discountId && p.TimesUsed > 0))
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.TimesUsed, p => p.TimesUsed - 1), ct);
}
