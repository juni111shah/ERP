using ERP.Modules.Crm;
using ERP.Modules.Crm.Domain;
using ERP.Modules.Crm.Features;
using Xunit;

namespace ERP.UnitTests;

public class CrmModuleTests
{
    [Fact]
    public void Crm_manifest_registers_expected_key_and_menu()
    {
        var module = new CrmModule();
        Assert.Equal("crm", module.Manifest.Key);
        Assert.Equal("CRM", module.Manifest.Name);
        Assert.Equal("Horizontal", module.Manifest.Category);

        var menu = module.Menu.ToList();
        Assert.Equal(4, menu.Count);
        Assert.Contains(menu, item => item.Route == "/crm/leads");
        Assert.Contains(menu, item => item.Route == "/crm/opportunities");
        Assert.Contains(menu, item => item.Route == "/crm/interactions");
        Assert.Contains(menu, item => item.Route == "/crm/analytics");
    }

    [Fact]
    public void Crm_permission_keys_follow_module_resource_action()
    {
        var module = new CrmModule();
        foreach (var def in module.Permissions)
        {
            Assert.Matches("^[a-z]+\\.[a-z]+\\.[a-z]+$", def.Key);
            Assert.Equal("crm", def.Module);
        }
    }

    [Fact]
    public void Create_lead_validator_enforces_rules()
    {
        var validator = new CreateLeadValidator();
        var bad = new CreateLeadCommand(Guid.Empty, "", null, null, "not-an-email", null, LeadSource.Website, -500, null, null, null, null);
        var badRes = validator.Validate(bad);
        Assert.False(badRes.IsValid);

        var good = new CreateLeadCommand(
            Guid.NewGuid(),
            "Sarah Jenkins",
            "Acme Global Holdings",
            "VP Procurement",
            "sarah.j@acme.example.com",
            "+971 50 123 4567",
            LeadSource.Referral,
            150000m,
            null,
            null,
            "Interested in enterprise suite rollout",
            null
        );
        var goodRes = validator.Validate(good);
        Assert.True(goodRes.IsValid);
    }

    [Fact]
    public void Create_opportunity_validator_enforces_rules()
    {
        var validator = new CreateOpportunityValidator();
        var bad = new CreateOpportunityCommand(Guid.Empty, "", null, "", OpportunityStage.Qualification, 120, -100, null, null, null, null, null);
        var badRes = validator.Validate(bad);
        Assert.False(badRes.IsValid);

        var good = new CreateOpportunityCommand(
            Guid.NewGuid(),
            "Acme Enterprise License & Support",
            null,
            "Acme Global Holdings",
            OpportunityStage.Proposal,
            60,
            120000m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            null,
            null,
            "RFP submitted on Monday",
            null
        );
        var goodRes = validator.Validate(good);
        Assert.True(goodRes.IsValid);
    }

    [Fact]
    public void Convert_lead_validator_enforces_rules()
    {
        var validator = new ConvertLeadValidator();
        var bad = new ConvertLeadCommand(Guid.Empty, "", -10, null);
        var badRes = validator.Validate(bad);
        Assert.False(badRes.IsValid);

        var good = new ConvertLeadCommand(Guid.NewGuid(), "New Deal from Lead", 75000m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)));
        var goodRes = validator.Validate(good);
        Assert.True(goodRes.IsValid);
    }
}
