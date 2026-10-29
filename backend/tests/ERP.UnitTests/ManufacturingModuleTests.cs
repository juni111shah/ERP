using ERP.Modules.Manufacturing;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Manufacturing.Features;
using Xunit;

namespace ERP.UnitTests;

public class ManufacturingModuleTests
{
    [Fact]
    public void Manufacturing_manifest_registers_expected_key_and_menu()
    {
        var module = new ManufacturingModule();
        Assert.Equal("manufacturing", module.Manifest.Key);
        Assert.Equal("Manufacturing", module.Manifest.Name);
        Assert.Equal("Horizontal", module.Manifest.Category);

        var menu = module.Menu.ToList();
        Assert.Equal(4, menu.Count);
        Assert.Contains(menu, item => item.Route == "/manufacturing/boms");
        Assert.Contains(menu, item => item.Route == "/manufacturing/workorders");
        Assert.Contains(menu, item => item.Route == "/manufacturing/costing");
        Assert.Contains(menu, item => item.Route == "/manufacturing/analytics");
    }

    [Fact]
    public void Manufacturing_permission_keys_follow_module_resource_action()
    {
        var module = new ManufacturingModule();
        foreach (var def in module.Permissions)
        {
            Assert.Matches("^[a-z]+\\.[a-z]+\\.[a-z]+$", def.Key);
            Assert.Equal("manufacturing", def.Module);
        }
    }

    [Fact]
    public void Create_bom_validator_enforces_rules()
    {
        var validator = new CreateBomValidator();
        var bad = new CreateBomCommand(Guid.Empty, "", "", Guid.Empty, "", "", 0, "", "", 0, 0, true, null, [], null);
        var badRes = validator.Validate(bad);
        Assert.False(badRes.IsValid);

        var good = new CreateBomCommand(
            Guid.NewGuid(),
            "BOM-CHAIR-01",
            "Executive Office Chair Recipe",
            Guid.NewGuid(),
            "CHAIR-EXEC-BLK",
            "Executive Office Chair",
            1,
            "PCS",
            "1.0",
            15m,
            10m,
            true,
            "Standard bill of materials",
            [
                new BomComponentInput(Guid.NewGuid(), "ARM-REST", "Arm Rest Pair", 2, "PCS", 0, 15m, null)
            ],
            null
        );
        var goodRes = validator.Validate(good);
        Assert.True(goodRes.IsValid);
    }

    [Fact]
    public void Create_work_order_validator_enforces_rules()
    {
        var validator = new CreateWorkOrderValidator();
        var bad = new CreateWorkOrderCommand(Guid.Empty, Guid.Empty, 0, Guid.Empty, DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), null, null, null);
        var badRes = validator.Validate(bad);
        Assert.False(badRes.IsValid);

        var good = new CreateWorkOrderCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            25,
            Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            null,
            null,
            null
        );
        var goodRes = validator.Validate(good);
        Assert.True(goodRes.IsValid);
    }
}
