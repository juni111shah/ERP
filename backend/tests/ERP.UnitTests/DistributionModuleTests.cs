using ERP.Modules.Distribution;
using ERP.Modules.Distribution.Domain;
using ERP.Modules.Distribution.Features;
using Xunit;

namespace ERP.UnitTests;

public class DistributionModuleTests
{
    [Fact]
    public void Distribution_manifest_registers_expected_key_and_menu()
    {
        var module = new DistributionModule();
        Assert.Equal("distribution", module.Manifest.Key);
        Assert.Equal("Distribution", module.Manifest.Name);
        Assert.Equal("Horizontal", module.Manifest.Category);

        var menu = module.Menu.ToList();
        Assert.Equal(4, menu.Count);
        Assert.Contains(menu, item => item.Route == "/distribution/routes");
        Assert.Contains(menu, item => item.Route == "/distribution/trips");
        Assert.Contains(menu, item => item.Route == "/distribution/wholesale");
        Assert.Contains(menu, item => item.Route == "/distribution/analytics");
    }

    [Fact]
    public void Distribution_permission_keys_follow_module_resource_action()
    {
        var module = new DistributionModule();
        foreach (var def in module.Permissions)
        {
            Assert.Matches("^[a-z]+\\.[a-z]+\\.[a-z]+$", def.Key);
            Assert.Equal("distribution", def.Module);
        }
    }

    [Fact]
    public void Create_route_validator_enforces_rules()
    {
        var validator = new CreateRouteValidator();
        var bad = new CreateRouteCommand(Guid.Empty, "", "", null, null, null, null, null, null, true, null, null);
        var badRes = validator.Validate(bad);
        Assert.False(badRes.IsValid);

        var good = new CreateRouteCommand(Guid.NewGuid(), "RT-DXB-01", "Dubai North Commercial Route", null, Guid.NewGuid(), "Tariq Driver", "DXB-7782", 2500, Guid.NewGuid(), true, null, null);
        var goodRes = validator.Validate(good);
        Assert.True(goodRes.IsValid);
    }

    [Fact]
    public void Create_trip_validator_enforces_rules()
    {
        var validator = new CreateTripValidator();
        var bad = new CreateTripCommand(Guid.Empty, Guid.Empty, Guid.Empty, "", "", Guid.Empty, DateOnly.FromDateTime(DateTime.UtcNow), null, [], null);
        var badRes = validator.Validate(bad);
        Assert.False(badRes.IsValid);

        var good = new CreateTripCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Hamad Driver",
            "DXB-5512",
            Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Morning wholesale route",
            [
                new TripInventoryInput(Guid.NewGuid(), "BEV-001", "Bottled Mineral Water 500ml", "BOX", 50, 24m)
            ],
            null);
        var goodRes = validator.Validate(good);
        Assert.True(goodRes.IsValid);
    }
}
