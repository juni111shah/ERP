using ERP.Modules.Pos;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Features;
using Xunit;

namespace ERP.UnitTests;

public class PosModuleTests
{
    [Fact]
    public void PosModule_manifest_and_permissions_are_valid()
    {
        var module = new PosModule();
        Assert.Equal("pos", module.Manifest.Key);
        Assert.Equal("Point of Sale", module.Manifest.Name);
        Assert.Equal("Horizontal", module.Manifest.Category);

        var perms = module.Permissions.Select(p => p.Key).ToList();
        Assert.Contains(Permissions.RegistersRead, perms);
        Assert.Contains(Permissions.RegistersManage, perms);
        Assert.Contains(Permissions.SessionsRead, perms);
        Assert.Contains(Permissions.SessionsManage, perms);
        Assert.Contains(Permissions.OrdersRead, perms);
        Assert.Contains(Permissions.OrdersCreate, perms);
        Assert.Contains(Permissions.OrdersVoid, perms);
        Assert.Contains(Permissions.AnalyticsRead, perms);

        foreach (var p in perms)
        {
            Assert.StartsWith("pos.", p);
        }
    }

    [Fact]
    public void PosModule_menu_items_are_configured()
    {
        var module = new PosModule();
        var menu = module.Menu.ToList();
        Assert.Equal(5, menu.Count);

        var routes = menu.Select(m => m.Route).ToList();
        Assert.Contains("/pos/terminal", routes);
        Assert.Contains("/pos/sessions", routes);
        Assert.Contains("/pos/orders", routes);
        Assert.Contains("/pos/registers", routes);
        Assert.Contains("/pos/analytics", routes);

        foreach (var m in menu)
        {
            Assert.Equal("pos", m.GroupId);
        }
    }

    [Fact]
    public void PosOrder_validation_rules()
    {
        var validator = new CreatePosOrderValidator();

        var invalid = new CreatePosOrderCommand(
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            null,
            null,
            Guid.Empty,
            "",
            [],
            [],
            null,
            null);

        var res = validator.Validate(invalid);
        Assert.False(res.IsValid);
        Assert.Contains(res.Errors, e => e.PropertyName == "LegalEntityId");
        Assert.Contains(res.Errors, e => e.PropertyName == "RegisterId");
        Assert.Contains(res.Errors, e => e.PropertyName == "SessionId");
        Assert.Contains(res.Errors, e => e.PropertyName == "Lines");
        Assert.Contains(res.Errors, e => e.PropertyName == "Payments");

        var valid = new CreatePosOrderCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "Walk-in Customer",
            Guid.NewGuid(),
            "Cashier 1",
            [
                new PosOrderLineInput(Guid.NewGuid(), "BEV-01", "Espresso Roast Coffee", 2, 18.50m, 0, 5),
            ],
            [
                new PosPaymentInput(PosPaymentMethod.Cash, 38.85m, 40m, 1.15m, null),
            ],
            "Table 4",
            null);

        Assert.True(validator.Validate(valid).IsValid);
    }

    [Fact]
    public void PosSession_open_and_close_validation_rules()
    {
        var openValidator = new OpenSessionValidator();
        var invalidOpen = new OpenSessionCommand(Guid.Empty, Guid.Empty, -10, Guid.NewGuid(), "", null);
        var openRes = openValidator.Validate(invalidOpen);
        Assert.False(openRes.IsValid);

        var validOpen = new OpenSessionCommand(Guid.NewGuid(), Guid.NewGuid(), 200, Guid.NewGuid(), "Jane Cashier", "Morning shift");
        Assert.True(openValidator.Validate(validOpen).IsValid);

        var closeValidator = new CloseSessionValidator();
        var invalidClose = new CloseSessionCommand(Guid.Empty, -5, Guid.NewGuid(), null);
        var closeRes = closeValidator.Validate(invalidClose);
        Assert.False(closeRes.IsValid);

        var validClose = new CloseSessionCommand(Guid.NewGuid(), 1450.50m, Guid.NewGuid(), "Balanced shift");
        Assert.True(closeValidator.Validate(validClose).IsValid);
    }

    [Fact]
    public void PosOrder_discount_above_100_is_rejected()
    {
        var validator = new CreatePosOrderValidator();
        var cmd = new CreatePosOrderCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "Customer",
            [
                new PosOrderLineInput(Guid.NewGuid(), "ITM-01", "Item 1", 1, 100m, 105m, 5m)
            ],
            [
                new PosPaymentInput(PosPaymentMethod.Cash, 100m, 100m, 0m, null)
            ]);

        var res = validator.Validate(cmd);
        Assert.False(res.IsValid);
        Assert.Contains(res.Errors, e => e.PropertyName.Contains("DiscountPercent"));
    }

    [Fact]
    public void PosOrder_totals_rounding_half_away_from_zero()
    {
        // 3 items @ 18.55 = 55.65. Discount 15% = 8.3475 -> 8.35.
        // After discount = 55.65 - 8.35 = 47.30.
        // Tax 5% = 47.30 * 0.05 = 2.365 -> 2.37.
        // Total = 47.30 + 2.37 = 49.67.
        decimal qty = 3m;
        decimal unitPrice = 18.55m;
        decimal discountPercent = 15m;
        decimal taxRate = 5m;

        var rawLine = ERP.Application.Finance.Money.Round(qty * unitPrice);
        Assert.Equal(55.65m, rawLine);

        var disc = ERP.Application.Finance.Money.Round(rawLine * (discountPercent / 100m));
        Assert.Equal(8.35m, disc);

        var afterDisc = rawLine - disc;
        Assert.Equal(47.30m, afterDisc);

        var tax = ERP.Application.Finance.Money.Round(afterDisc * (taxRate / 100m));
        Assert.Equal(2.37m, tax);

        var lineTotal = afterDisc + tax;
        Assert.Equal(49.67m, lineTotal);
    }
}
