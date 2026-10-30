using ERP.Application.Common;
using ERP.Modules.Purchasing.Features;
using Xunit;

namespace ERP.UnitTests;

public class PurchaseOrderRulesTests
{
    private static readonly Guid Line = Guid.NewGuid();

    [Fact] public void No_overrides_are_valid() => Assert.True(PurchaseOrderBillRules.AreValidOverrides(null));
    [Fact] public void Empty_overrides_are_valid() => Assert.True(PurchaseOrderBillRules.AreValidOverrides(new Dictionary<Guid, decimal>()));
    [Fact] public void Zero_and_four_decimal_prices_are_valid() => Assert.True(PurchaseOrderBillRules.AreValidOverrides(new Dictionary<Guid, decimal> { [Line] = 0m, [Guid.NewGuid()] = 12.3456m }));
    [Fact] public void A_negative_price_is_invalid() => Assert.False(PurchaseOrderBillRules.AreValidOverrides(new Dictionary<Guid, decimal> { [Line] = -0.01m }));
    [Fact] public void A_five_decimal_price_is_invalid() => Assert.False(PurchaseOrderBillRules.AreValidOverrides(new Dictionary<Guid, decimal> { [Line] = 1.12345m }));

    [Fact]
    public void Converting_a_purchase_order_is_retryable_on_concurrency_conflicts()
        => Assert.True(typeof(IRetryOnConcurrency).IsAssignableFrom(typeof(ConvertPurchaseOrderToBill)));

    [Fact]
    public void The_convert_validator_rejects_bad_overrides_on_the_unit_price_overrides_key()
    {
        var result = new ConvertPurchaseOrderToBillValidator().Validate(new ConvertPurchaseOrderToBill(Guid.NewGuid(), new Dictionary<Guid, decimal> { [Line] = -5m }));
        Assert.Contains(result.Errors, e => e.PropertyName == "UnitPriceOverrides");
        Assert.True(new ConvertPurchaseOrderToBillValidator().Validate(new ConvertPurchaseOrderToBill(Guid.NewGuid())).IsValid);
    }

    private static PurchaseOrderInput Input(string orderDate = "2026-05-01", string? delivery = null, decimal quantity = 1m, decimal price = 10m)
        => new(Guid.NewGuid(), null, null, Guid.NewGuid(), "Vendor", null, null, DateOnly.Parse(orderDate), delivery is null ? null : DateOnly.Parse(delivery), "AED", null, null, null,
            [new PurchaseOrderLineInput(null, "X", "Item", quantity, "EA", price, 0m, 0m)]);

    [Fact] public void A_normal_order_is_valid() => Assert.True(new PurchaseOrderInputValidator().Validate(Input(delivery: "2026-06-01", quantity: 1.2345m, price: 9.1234m)).IsValid);

    [Theory]
    [InlineData("0001-01-01", null, "OrderDate")]
    [InlineData("1999-12-31", null, "OrderDate")]
    [InlineData("2099-01-01", null, "OrderDate")]
    [InlineData("2026-05-10", "2026-05-01", "expectedDeliveryDate")]
    [InlineData("2026-05-10", "0001-01-01", "ExpectedDeliveryDate")]
    public void Implausible_or_inconsistent_dates_are_rejected(string orderDate, string? delivery, string property)
    {
        var result = new PurchaseOrderInputValidator().Validate(Input(orderDate, delivery));
        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    [Fact]
    public void More_than_four_decimals_on_quantity_or_price_are_rejected()
    {
        Assert.False(new PurchaseOrderInputValidator().Validate(Input(quantity: 1.00001m)).IsValid);
        Assert.False(new PurchaseOrderInputValidator().Validate(Input(price: 9.12345m)).IsValid);
    }
}
