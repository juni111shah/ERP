using ERP.Modules.Retail;
using ERP.Modules.Retail.Domain;
using ERP.Modules.Retail.Features;
using Xunit;

namespace ERP.UnitTests;

public class RetailModuleTests
{
    [Fact]
    public void Retail_manifest_registers_expected_key_and_menu()
    {
        var module = new RetailModule();
        Assert.Equal("retail", module.Manifest.Key);
        Assert.Equal("Retail", module.Manifest.Name);
        Assert.Equal("Horizontal", module.Manifest.Category);

        var menu = module.Menu.ToList();
        Assert.Equal(4, menu.Count);
        Assert.Contains(menu, item => item.Route == "/retail/stores");
        Assert.Contains(menu, item => item.Route == "/retail/pricelists");
        Assert.Contains(menu, item => item.Route == "/retail/promotions");
        Assert.Contains(menu, item => item.Route == "/retail/analytics");
    }

    [Fact]
    public void Retail_permission_keys_follow_module_resource_action()
    {
        var module = new RetailModule();
        foreach (var def in module.Permissions)
        {
            Assert.Matches("^[a-z]+\\.[a-z]+\\.[a-z]+$", def.Key);
            Assert.Equal("retail", def.Module);
        }
    }

    [Fact]
    public void Create_store_command_validator_enforces_rules()
    {
        var validator = new CreateStoreValidator();
        var bad = new CreateStoreCommand(Guid.Empty, "", "", null, null, null, null, null, true, null);
        var badRes = validator.Validate(bad);
        Assert.False(badRes.IsValid);

        var good = new CreateStoreCommand(Guid.NewGuid(), "STR-001", "Main Street Flagship", null, "John Manager", null, null, null, true, null);
        var goodRes = validator.Validate(good);
        Assert.True(goodRes.IsValid);
    }

    [Fact]
    public void Create_promotion_command_validator_enforces_rules()
    {
        var validator = new CreatePromotionValidator();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var bad = new CreatePromotionCommand(Guid.Empty, "", "", PromotionType.PercentageDiscount, -5, null, today, today.AddDays(-1), null, true, null, null);
        var badRes = validator.Validate(bad);
        Assert.False(badRes.IsValid);

        var good = new CreatePromotionCommand(Guid.NewGuid(), "SUMMER15", "Summer Flash 15%", PromotionType.PercentageDiscount, 15m, 50m, today, today.AddDays(10), 500, true, "Promo note", null);
        var goodRes = validator.Validate(good);
        Assert.True(goodRes.IsValid);
    }
}
