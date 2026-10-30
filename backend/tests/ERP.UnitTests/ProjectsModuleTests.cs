using ERP.Modules.Projects;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Features;
using Xunit;

namespace ERP.UnitTests;

public class ProjectsModuleTests
{
    [Fact]
    public void ProjectsModule_manifest_and_permissions_are_valid()
    {
        var module = new ProjectsModule();
        Assert.Equal("projects", module.Manifest.Key);
        Assert.Equal("Projects", module.Manifest.Name);
        Assert.Equal("Horizontal", module.Manifest.Category);

        var perms = module.Permissions.Select(p => p.Key).ToList();
        Assert.Contains(P.ProjectsRead, perms);
        Assert.Contains(P.ProjectsManage, perms);
        Assert.Contains(P.TasksRead, perms);
        Assert.Contains(P.TasksManage, perms);
        Assert.Contains(P.TimesheetsRead, perms);
        Assert.Contains(P.TimesheetsManage, perms);
        Assert.Contains(P.MilestonesRead, perms);
        Assert.Contains(P.MilestonesManage, perms);
        Assert.Contains(P.BillingRead, perms);
        Assert.Contains(P.BillingManage, perms);

        foreach (var p in perms)
        {
            Assert.StartsWith("projects.", p);
            Assert.True(p.EndsWith(".read") || p.EndsWith(".manage"));
        }
    }

    [Fact]
    public void ProjectsModule_menu_items_are_configured()
    {
        var module = new ProjectsModule();
        var menu = module.Menu.ToList();
        Assert.Equal(4, menu.Count);

        var routes = menu.Select(m => m.Route).ToList();
        Assert.Contains("/projects/list", routes);
        Assert.Contains("/projects/tasks", routes);
        Assert.Contains("/projects/timesheets", routes);
        Assert.Contains("/projects/billing", routes);

        foreach (var m in menu)
        {
            Assert.Equal("projects", m.GroupId);
            Assert.Equal("Projects", m.GroupLabel);
        }
    }

    [Fact]
    public void Project_input_validation_rules()
    {
        var validator = new ProjectInputValidator();

        var invalid = new ProjectInput(
            Guid.Empty,
            "",
            "",
            null,
            null,
            null,
            null,
            ProjectStatus.Planning,
            ProjectBillingType.TimeAndMaterials,
            -10,
            new DateOnly(2026, 12, 31),
            new DateOnly(2026, 1, 1));

        var res = validator.Validate(invalid);
        Assert.False(res.IsValid);
        Assert.Contains(res.Errors, e => e.PropertyName == "LegalEntityId");
        Assert.Contains(res.Errors, e => e.PropertyName == "Name");
        Assert.Contains(res.Errors, e => e.PropertyName == "BudgetAmount");
        Assert.Contains(res.Errors, e => e.ErrorMessage.Contains("End date cannot be earlier than start date"));

        var valid = new ProjectInput(
            Guid.NewGuid(),
            "PRJ-0001",
            "Enterprise Implementation",
            "Phase 1 deployment",
            null,
            "Acme Corp",
            "Alice",
            ProjectStatus.Active,
            ProjectBillingType.TimeAndMaterials,
            50000,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30));

        Assert.True(validator.Validate(valid).IsValid);
    }

    [Fact]
    public void Timesheet_input_validation_rules()
    {
        var validator = new TimesheetInputValidator();

        var invalid = new TimesheetInput(
            Guid.Empty,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "",
            0,
            -5,
            null);

        var res = validator.Validate(invalid);
        Assert.False(res.IsValid);
        Assert.Contains(res.Errors, e => e.PropertyName == "ProjectId");
        Assert.Contains(res.Errors, e => e.PropertyName == "WorkerName");
        Assert.Contains(res.Errors, e => e.PropertyName == "Hours");
        Assert.Contains(res.Errors, e => e.PropertyName == "HourlyRate");

        var valid = new TimesheetInput(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.UtcNow),
            "John Doe",
            8.5m,
            120,
            "Developed database migrations");

        Assert.True(validator.Validate(valid).IsValid);
    }

    [Fact]
    public void Milestone_input_validation_rules()
    {
        var validator = new MilestoneInputValidator();

        var invalid = new MilestoneInput(Guid.Empty, "", DateOnly.FromDateTime(DateTime.UtcNow), -100);
        var res = validator.Validate(invalid);
        Assert.False(res.IsValid);
        Assert.Contains(res.Errors, e => e.PropertyName == "ProjectId");
        Assert.Contains(res.Errors, e => e.PropertyName == "Title");
        Assert.Contains(res.Errors, e => e.PropertyName == "Amount");

        var valid = new MilestoneInput(Guid.NewGuid(), "Design Sign-off", DateOnly.FromDateTime(DateTime.UtcNow), 15000);
        Assert.True(validator.Validate(valid).IsValid);
    }
}
