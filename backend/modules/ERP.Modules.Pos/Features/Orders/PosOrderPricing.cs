using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

/// <summary>
/// The server's own prices, tax, discount limits and discount codes for till lines. Used by sale, quote and hold, so a resumed
/// ticket is priced again at payment.
/// </summary>
public sealed class PosOrderPricing(
    IPosItemRepository items,
    IPosTaxCodeRepository taxCodes,
    ISettingsService settings,
    ICurrentUser currentUser,
    IEnumerable<IPriceResolver> priceResolvers)
{
    private readonly IPriceResolver? _priceResolver = priceResolvers.FirstOrDefault();

    public sealed record PricedLines(List<PosOrderLine> Lines, decimal SubTotal, decimal TaxTotal, decimal DiscountTotal, DiscountCodeResult? Promo = null)
    {
        public decimal Total => SubTotal + TaxTotal;
    }

    private static DateOnly Today => DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);

    /// <summary>The price resolver that redeems and releases discount codes; null when none is installed.</summary>
    public IPriceResolver? PriceResolver => _priceResolver;

    /// <summary>Takes a discount code off the priced lines, before tax, sharing it across them by value with the last line taking the rounding. Nothing is used up here.</summary>
    public async Task<PricedLines> ApplyPromoAsync(Guid legalEntityId, PricedLines priced, string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return priced;
        if (_priceResolver is null) throw new FieldValidationException("promoCode", "Discount codes are not available here.");

        var promo = await _priceResolver.QuoteDiscountAsync(new DiscountCodeQuery(legalEntityId, Today, code, priced.SubTotal, priced.Total), ct);
        var amount = Math.Min(promo.Amount, priced.SubTotal);
        decimal shared = 0, subtotal = 0, tax = 0;
        var discounted = priced.Lines.Where(l => PosMath.Line(l).Net > 0).ToList();
        foreach (var line in discounted)
        {
            var share = ReferenceEquals(line, discounted[^1]) ? amount - shared : Money.Round(amount * PosMath.Line(line).Net / priced.SubTotal);
            line.PromoDiscount = share;
            shared += share;
        }
        foreach (var line in priced.Lines)
        {
            var a = PosMath.Line(line);
            line.LineTotal = a.Total;
            subtotal += a.Net;
            tax += a.Tax;
        }
        return new PricedLines(priced.Lines, subtotal, tax, priced.DiscountTotal + amount, promo with { Amount = amount });
    }

    /// <summary>The server's own prices, tax and discount limits. Sales and held tickets both go through here, so a resumed ticket is priced again at payment.</summary>
    public async Task<PricedLines> PriceAsync(Guid legalEntityId, Guid? customerId, IReadOnlyList<PosOrderLineInput> input, CancellationToken ct)
    {
        var maxDiscount = await settings.GetAsync<decimal?>("pos.maxDiscountPercent", legalEntityId, ct) ?? 100m;
        var cashierLimit = await settings.GetAsync<decimal?>("pos.cashierDiscountPercent", legalEntityId, ct);
        var canOverridePrice = currentUser.HasPermission(Permissions.OrdersOverridePrice);
        var limit = cashierLimit is { } c && !currentUser.HasPermission(Permissions.OrdersDiscountOverride) ? Math.Min(c, maxDiscount) : maxDiscount;

        var itemIds = input.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToList();
        var itemsById = itemIds.Count > 0
            ? await items.MapByIdsAsync(itemIds, ct)
            : new Dictionary<Guid, Item>();

        var taxCodeIds = itemsById.Values.Where(i => i.TaxCodeId.HasValue).Select(i => i.TaxCodeId!.Value).Distinct().ToList();
        var taxCodesById = taxCodeIds.Count > 0
            ? await taxCodes.MapByIdsAsync(taxCodeIds, ct)
            : new Dictionary<Guid, TaxCode>();

        // A price list, where one applies, replaces the catalogue price as the price the till charges and the floor for anyone who cannot override.
        var listPrices = _priceResolver is null || itemIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await _priceResolver.GetPricesAsync(new PriceQuery(legalEntityId, Today, customerId,
                input.Where(l => l.ItemId.HasValue).GroupBy(l => l.ItemId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity))), ct);

        decimal subtotal = 0, taxTotal = 0, discountTotal = 0;
        var lines = new List<PosOrderLine>();
        foreach (var l in input)
        {
            if (l.DiscountPercent < 0 || l.DiscountPercent > 100)
            {
                throw new FieldValidationException("discountPercent", "Discount must be between 0 and 100.");
            }
            if (l.DiscountPercent > limit)
            {
                var who = limit < maxDiscount ? " for your role; ask someone who can approve a larger discount" : string.Empty;
                throw new FieldValidationException("discountPercent", $"Discount ({l.DiscountPercent}%) exceeds maximum allowed ({limit}%){who}.");
            }

            decimal unitPrice;
            decimal taxRate;
            var itemCode = l.ItemCode ?? string.Empty;
            var description = l.Description ?? string.Empty;

            if (l.ItemId.HasValue)
            {
                if (!itemsById.TryGetValue(l.ItemId.Value, out var item))
                {
                    throw new NotFoundException($"Item {l.ItemId.Value} not found.");
                }
                if (!item.IsActive)
                {
                    throw new ConflictException($"{item.Name} is inactive and cannot be sold.");
                }

                itemCode = string.IsNullOrWhiteSpace(itemCode) ? item.Sku : itemCode;
                description = string.IsNullOrWhiteSpace(description) ? item.Name : description;

                var hasListPrice = listPrices.TryGetValue(item.Id, out var listPrice);
                if (item.SalesPrice is null && !hasListPrice && !(canOverridePrice && l.UnitPrice.HasValue))
                {
                    throw new FieldValidationException("unitPrice", $"{item.Name} has no sales price.");
                }
                var catalogPrice = hasListPrice ? listPrice : item.SalesPrice ?? 0m;

                if (l.UnitPrice.HasValue)
                {
                    if (l.UnitPrice.Value < catalogPrice && !canOverridePrice)
                    {
                        throw new FieldValidationException("unitPrice", $"Posting a lower unit price ({l.UnitPrice.Value:N2}) than item price ({catalogPrice:N2}) requires override permission.");
                    }
                    unitPrice = canOverridePrice ? l.UnitPrice.Value : catalogPrice;
                }
                else
                {
                    unitPrice = catalogPrice;
                }

                if (item.TaxCodeId.HasValue && taxCodesById.TryGetValue(item.TaxCodeId.Value, out var tc))
                {
                    taxRate = tc.Rate;
                }
                else
                {
                    taxRate = canOverridePrice ? l.TaxRate ?? 0m : 0m;
                }
            }
            else
            {
                if (!canOverridePrice)
                {
                    throw new FieldValidationException("unitPrice", "Selling a free-text item at a manual price requires override permission.");
                }
                unitPrice = l.UnitPrice ?? 0m;
                taxRate = l.TaxRate ?? 0m;
            }

            var a = PosMath.Line(l.Quantity, unitPrice, l.DiscountPercent, taxRate);
            subtotal += a.Net;
            taxTotal += a.Tax;
            discountTotal += a.Discount;

            lines.Add(new PosOrderLine
            {
                ItemId = l.ItemId,
                ItemCode = itemCode.Trim(),
                Description = description.Trim(),
                Quantity = l.Quantity,
                UnitPrice = unitPrice,
                DiscountPercent = l.DiscountPercent,
                TaxRate = taxRate,
                LineTotal = a.Total
            });
        }

        return new PricedLines(lines, subtotal, taxTotal, discountTotal);
    }
}
