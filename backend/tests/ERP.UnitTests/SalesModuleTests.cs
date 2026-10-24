using ERP.Modules.Sales;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Features;
using Xunit;

namespace ERP.UnitTests;

public class SalesModuleTests
{
    [Fact]
    public void SalesModule_manifest_and_permissions_are_valid()
    {
        var module = new SalesModule();
        Assert.Equal("sales", module.Manifest.Key);
        Assert.Equal("Sales", module.Manifest.Name);
        Assert.Equal("Horizontal", module.Manifest.Category);

        var perms = module.Permissions.Select(p => p.Key).ToList();
        Assert.Contains(P.QuotesRead, perms);
        Assert.Contains(P.QuotesManage, perms);
        Assert.Contains(P.OrdersRead, perms);
        Assert.Contains(P.OrdersManage, perms);
        Assert.Contains(P.AnalyticsRead, perms);

        foreach (var p in perms)
        {
            Assert.StartsWith("sales.", p);
            Assert.True(p.EndsWith(".read") || p.EndsWith(".manage"));
        }
    }

    [Fact]
    public void SalesModule_menu_items_are_configured()
    {
        var module = new SalesModule();
        var menu = module.Menu.ToList();
        Assert.Equal(3, menu.Count);

        var routes = menu.Select(m => m.Route).ToList();
        Assert.Contains("/sales/quotes", routes);
        Assert.Contains("/sales/orders", routes);
        Assert.Contains("/sales/analytics", routes);

        foreach (var m in menu)
        {
            Assert.Equal("sales", m.GroupId);
            Assert.Equal("Sales", m.GroupLabel);
        }
    }

    [Fact]
    public void Quote_input_validation_rules()
    {
        var validator = new QuoteInputValidator();

        var invalid = new QuoteInput(
            Guid.Empty,
            "",
            null,
            "",
            null,
            null,
            new DateOnly(2026, 1, 1),
            null,
            "",
            null,
            null,
            []);

        var res = validator.Validate(invalid);
        Assert.False(res.IsValid);
        Assert.Contains(res.Errors, e => e.PropertyName == "LegalEntityId");
        Assert.Contains(res.Errors, e => e.PropertyName == "CustomerName");
        Assert.Contains(res.Errors, e => e.PropertyName == "Currency");
        Assert.Contains(res.Errors, e => e.PropertyName == "Lines");

        var valid = new QuoteInput(
            Guid.NewGuid(),
            "QT-0001",
            Guid.NewGuid(),
            "Global Trading LLC",
            "contact@global.local",
            "+971501234567",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1),
            "AED",
            "Valid for 30 days",
            "50% advance, balance on delivery",
            [
                new QuoteLineInput(Guid.NewGuid(), "PROD-01", "Enterprise Server", 2, "EA", 12000, 5, 5),
            ]);

        Assert.True(validator.Validate(valid).IsValid);
    }

    [Fact]
    public void Order_input_validation_rules()
    {
        var validator = new OrderInputValidator();

        var invalid = new OrderInput(
            Guid.Empty,
            "",
            null,
            null,
            "",
            null,
            new DateOnly(2026, 1, 1),
            null,
            "",
            null,
            null,
            null,
            []);

        var res = validator.Validate(invalid);
        Assert.False(res.IsValid);
        Assert.Contains(res.Errors, e => e.PropertyName == "LegalEntityId");
        Assert.Contains(res.Errors, e => e.PropertyName == "CustomerName");
        Assert.Contains(res.Errors, e => e.PropertyName == "Currency");
        Assert.Contains(res.Errors, e => e.PropertyName == "Lines");

        var valid = new OrderInput(
            Guid.NewGuid(),
            "SO-0001",
            null,
            Guid.NewGuid(),
            "Global Trading LLC",
            "PO-9982",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 15),
            "AED",
            "Net 30",
            "Warehouse 4, Al Quoz, Dubai",
            "Handle with care",
            [
                new OrderLineInput(Guid.NewGuid(), "PROD-01", "Enterprise Server", 2, "EA", 12000, 5, 5),
            ]);

        Assert.True(validator.Validate(valid).IsValid);
    }
}
