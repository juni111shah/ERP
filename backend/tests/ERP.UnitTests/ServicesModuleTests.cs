using ERP.Modules.Services;
using ERP.Modules.Services.Domain;
using ERP.Modules.Services.Features;
using Xunit;

namespace ERP.UnitTests;

public class ServicesModuleTests
{
    [Fact]
    public void Services_manifest_registers_expected_key_and_menu()
    {
        var module = new ServicesModule();
        Assert.Equal("services", module.Manifest.Key);
        Assert.Equal("Professional services", module.Manifest.Name);
        Assert.Equal("Industry", module.Manifest.Category);

        var menu = module.Menu.ToList();
        Assert.Equal(4, menu.Count);
        Assert.Contains(menu, item => item.Route == "/services/engagements");
        Assert.Contains(menu, item => item.Route == "/services/retainers");
        Assert.Contains(menu, item => item.Route == "/services/milestones");
        Assert.Contains(menu, item => item.Route == "/services/analytics");
    }

    [Fact]
    public void Services_permission_keys_follow_module_resource_action()
    {
        var module = new ServicesModule();
        foreach (var def in module.Permissions)
        {
            Assert.Matches("^[a-z]+\\.[a-z]+\\.[a-z]+$", def.Key);
            Assert.Equal("services", def.Module);
        }
    }

    [Fact]
    public void Create_engagement_validator_enforces_rules()
    {
        var validator = new CreateEngagementValidator();
        var bad = new CreateEngagementCommand(Guid.Empty, "", Guid.Empty, "", EngagementType.FixedPrice, -100, -10, DateOnly.FromDateTime(DateTime.UtcNow), null, null, null, null, null);
        var badRes = validator.Validate(bad);
        Assert.False(badRes.IsValid);

        var good = new CreateEngagementCommand(
            Guid.NewGuid(),
            "Cloud Transformation Advisory",
            Guid.NewGuid(),
            "Apex Global Corp",
            EngagementType.TimeAndMaterials,
            85000m,
            400m,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
            null,
            "Alex Carter",
            "Enterprise cloud migration oversight",
            null
        );
        var goodRes = validator.Validate(good);
        Assert.True(goodRes.IsValid);
    }

    [Fact]
    public void Create_retainer_validator_enforces_rules()
    {
        var validator = new CreateRetainerValidator();
        var bad = new CreateRetainerCommand(Guid.Empty, Guid.Empty, Guid.Empty, "", "", RetainerCycle.Monthly, -500, -1, -10, DateOnly.FromDateTime(DateTime.UtcNow), null, null, null);
        var badRes = validator.Validate(bad);
        Assert.False(badRes.IsValid);

        var good = new CreateRetainerCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Apex Global Corp",
            "Executive DevOps Retainer",
            RetainerCycle.Monthly,
            12000m,
            40m,
            250m,
            DateOnly.FromDateTime(DateTime.UtcNow),
            null,
            "Standard SLA terms apply",
            null
        );
        var goodRes = validator.Validate(good);
        Assert.True(goodRes.IsValid);
    }

    [Fact]
    public void Create_milestone_validator_enforces_rules()
    {
        var validator = new CreateMilestoneValidator();
        var bad = new CreateMilestoneCommand(Guid.Empty, Guid.Empty, "", -50, DateOnly.FromDateTime(DateTime.UtcNow), null, null);
        Assert.False(validator.Validate(bad).IsValid);

        var good = new CreateMilestoneCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Architecture Review Deliverable",
            25000m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            "Sign-off upon document submission",
            null
        );
        Assert.True(validator.Validate(good).IsValid);
    }

    [Fact]
    public void Log_time_entry_validator_enforces_rules()
    {
        var validator = new LogTimeEntryValidator();
        var bad = new LogTimeEntryCommand(Guid.Empty, Guid.Empty, Guid.Empty, "", DateOnly.FromDateTime(DateTime.UtcNow), -5, -100, "", true, null);
        Assert.False(validator.Validate(bad).IsValid);

        var good = new LogTimeEntryCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Alex Carter",
            DateOnly.FromDateTime(DateTime.UtcNow),
            7.5m,
            200m,
            "Conducted infrastructure audit and security vulnerability scan",
            true,
            null
        );
        Assert.True(validator.Validate(good).IsValid);
    }
}
