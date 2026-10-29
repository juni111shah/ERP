using ERP.Modules.Purchasing;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Features;
using Xunit;

namespace ERP.UnitTests;

public class PurchasingModuleTests
{
    [Fact]
    public void PurchasingModule_manifest_and_permissions_are_valid()
    {
        var module = new PurchasingModule();
        Assert.Equal("purchasing", module.Manifest.Key);
        Assert.Equal("Purchasing", module.Manifest.Name);
        Assert.Equal("Horizontal", module.Manifest.Category);

        var perms = module.Permissions.Select(p => p.Key).ToList();
        Assert.Contains(P.OrdersRead, perms);
        Assert.Contains(P.OrdersManage, perms);
        Assert.Contains(P.RequisitionsRead, perms);
        Assert.Contains(P.RequisitionsManage, perms);
        Assert.Contains(P.ReceiptsRead, perms);
        Assert.Contains(P.ReceiptsManage, perms);
        Assert.Contains(P.AnalyticsRead, perms);

        foreach (var p in perms)
        {
            Assert.StartsWith("purchasing.", p);
            Assert.True(p.EndsWith(".read") || p.EndsWith(".manage"));
        }
    }

    [Fact]
    public void PurchasingModule_menu_items_are_configured()
    {
        var module = new PurchasingModule();
        var menu = module.Menu.ToList();
        Assert.Equal(4, menu.Count);

        var routes = menu.Select(m => m.Route).ToList();
        Assert.Contains("/purchasing/orders", routes);
        Assert.Contains("/purchasing/requisitions", routes);
        Assert.Contains("/purchasing/receipts", routes);
        Assert.Contains("/purchasing/analytics", routes);

        foreach (var m in menu)
        {
            Assert.Equal("purchasing", m.GroupId);
            Assert.Equal("Purchasing", m.GroupLabel);
        }
    }

    [Fact]
    public void PurchaseOrder_input_validation_rules()
    {
        var validator = new PurchaseOrderInputValidator();

        var invalid = new PurchaseOrderInput(
            Guid.Empty,
            "",
            null,
            null,
            "",
            null,
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
        Assert.Contains(res.Errors, e => e.PropertyName == "VendorName");
        Assert.Contains(res.Errors, e => e.PropertyName == "Currency");
        Assert.Contains(res.Errors, e => e.PropertyName == "Lines");

        var valid = new PurchaseOrderInput(
            Guid.NewGuid(),
            "PO-0001",
            null,
            Guid.NewGuid(),
            "Industrial Supplies Co",
            "sales@industrial.local",
            "QUOTE-1234",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 20),
            "AED",
            "Net 30",
            "Warehouse 1",
            "Urgent delivery requested",
            [
                new PurchaseOrderLineInput(Guid.NewGuid(), "RAW-01", "Steel Sheets 2mm", 100, "EA", 85, 0, 5),
            ]);

        Assert.True(validator.Validate(valid).IsValid);
    }

    [Fact]
    public void Requisition_input_validation_rules()
    {
        var validator = new RequisitionInputValidator();

        var invalid = new RequisitionInput(
            Guid.Empty,
            "",
            "",
            null,
            new DateOnly(2026, 1, 1),
            null,
            null,
            []);

        var res = validator.Validate(invalid);
        Assert.False(res.IsValid);
        Assert.Contains(res.Errors, e => e.PropertyName == "LegalEntityId");
        Assert.Contains(res.Errors, e => e.PropertyName == "RequesterName");
        Assert.Contains(res.Errors, e => e.PropertyName == "Lines");

        var valid = new RequisitionInput(
            Guid.NewGuid(),
            "PR-0001",
            "Sarah Jenkins",
            "Operations",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 15),
            "Stock replenishment",
            [
                new RequisitionLineInput(Guid.NewGuid(), "OFF-01", "Ergonomic Chairs", 5, "EA", 450),
            ]);

        Assert.True(validator.Validate(valid).IsValid);
    }

    [Fact]
    public void GoodsReceipt_input_validation_rules()
    {
        var validator = new GoodsReceiptInputValidator();

        var invalid = new GoodsReceiptInput(
            Guid.Empty,
            "",
            Guid.Empty,
            "",
            new DateOnly(2026, 1, 1),
            null,
            null,
            []);

        var res = validator.Validate(invalid);
        Assert.False(res.IsValid);
        Assert.Contains(res.Errors, e => e.PropertyName == "LegalEntityId");
        Assert.Contains(res.Errors, e => e.PropertyName == "PurchaseOrderId");
        Assert.Contains(res.Errors, e => e.PropertyName == "ReceivedBy");
        Assert.Contains(res.Errors, e => e.PropertyName == "Lines");

        var valid = new GoodsReceiptInput(
            Guid.NewGuid(),
            "GRN-0001",
            Guid.NewGuid(),
            "Warehouse Receiver",
            new DateOnly(2026, 1, 1),
            "DN-9941",
            "All items received in good condition",
            [
                new GoodsReceiptLineInput(Guid.NewGuid(), Guid.NewGuid(), "RAW-01", "Steel Sheets 2mm", 100, "EA"),
            ]);

        Assert.True(validator.Validate(valid).IsValid);
    }
}
