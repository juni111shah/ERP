using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Application.Dashboard;
using ERP.Application.Search;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Modularity;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Projects;

public sealed class ProjectsModule : ModuleBase
{
    public const string Key = "projects";

    public override ModuleManifest Manifest { get; } = new(Key, "Projects", "Projects, tasks, timesheets and project billing.", "Horizontal");

    public override IEnumerable<PermissionDefinition> Permissions =>
    [
        PermissionDefinition.Of(P.ProjectsRead, "View projects"),
        PermissionDefinition.Of(P.ProjectsManage, "Create, edit, and delete projects"),
        PermissionDefinition.Of(P.TasksRead, "View project tasks"),
        PermissionDefinition.Of(P.TasksManage, "Create, edit, and update project tasks"),
        PermissionDefinition.Of(P.TimesheetsRead, "View timesheet entries"),
        PermissionDefinition.Of(P.TimesheetsManage, "Log and manage timesheet entries"),
        PermissionDefinition.Of(P.MilestonesRead, "View project milestones"),
        PermissionDefinition.Of(P.MilestonesManage, "Create, update, and achieve project milestones"),
        PermissionDefinition.Of(P.BillingRead, "View project billing summary"),
        PermissionDefinition.Of(P.BillingManage, "Generate project billing and invoice items"),
    ];

    public override IEnumerable<MenuItemDefinition> Menu =>
    [
        new("projects.projects", "Projects", "/projects/list", "project", P.ProjectsRead, 10, "projects", "Projects", 350),
        new("projects.tasks", "Tasks", "/projects/tasks", "check-square", P.TasksRead, 20, "projects", "Projects", 350),
        new("projects.timesheets", "Timesheets", "/projects/timesheets", "clock-circle", P.TimesheetsRead, 30, "projects", "Projects", 350),
        new("projects.billing", "Project billing", "/projects/billing", "dollar-circle", P.BillingRead, 40, "projects", "Projects", 350),
    ];

    public override IEnumerable<SettingDefinition> Settings =>
    [
        new("projects.defaultHourlyRate", "Default billable rate per hour", "150", "number"),
        new("projects.requireMilestoneApproval", "Milestones require approval before billing", "false", "bool"),
    ];

    public override IEnumerable<CustomizableEntityDefinition> CustomizableEntities =>
        [new(Docs.Project, "Project", typeof(Project), Key, P.ProjectsRead)];

    public override IEnumerable<IndustryTemplate> IndustryTemplates =>
    [
        new("services", "Professional services", "Projects, task tracking and project billing.", IndustryAvailability.Available, [Key],
            Roles:
            [
                new("Project manager", "Projects, tasks, milestones, timesheets and billing",
                    [P.ProjectsRead, P.ProjectsManage, P.TasksRead, P.TasksManage, P.TimesheetsRead, P.TimesheetsManage,
                     P.MilestonesRead, P.MilestonesManage, P.BillingRead, P.BillingManage, CorePermissions.ReportsRead], Key),
            ],
            Sequences:
            [
                new(Docs.Project, "PRJ", "{prefix}-{seq:4}", false),
                new("task", "TSK", "{prefix}-{seq:5}", false),
            ],
            SortOrder: 50),
    ];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDashboardContributor, ProjectsDashboard>();
        services.AddScoped<ISearchProvider, ProjectsSearch>();
        services.AddScoped<ERP.Application.Workflow.IAttachmentOwnerResolver>(sp =>
            new ERP.Application.Workflow.EntityAttachmentOwnerResolver<Project>(
                sp.GetRequiredService<ERP.Application.Abstractions.IEntityStore>(), "project", P.ProjectsRead, P.ProjectsManage));
    }

    public override void ConfigureModel(ModelBuilder mb)
    {
        mb.Entity<Project>(b =>
        {
            b.ToTable("prj_Projects");
            b.Property(x => x.Number).HasMaxLength(30).IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.CustomerName).HasMaxLength(200);
            b.Property(x => x.ManagerName).HasMaxLength(150);
            b.Property(x => x.BudgetAmount).HasPrecision(19, 4);
            b.HasIndex(x => new { x.OrgId, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Status });
            b.HasMany(x => x.Milestones).WithOne().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Tasks).WithOne().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Timesheets).WithOne().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<ProjectMilestone>(b =>
        {
            b.ToTable("prj_Milestones");
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Amount).HasPrecision(19, 4);
            b.HasIndex(x => new { x.OrgId, x.ProjectId, x.Status });
        });

        mb.Entity<ProjectTask>(b =>
        {
            b.ToTable("prj_Tasks");
            b.Property(x => x.Number).HasMaxLength(30).IsRequired();
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.AssignedTo).HasMaxLength(100);
            b.Property(x => x.EstimatedHours).HasPrecision(9, 2);
            b.Property(x => x.ActualHours).HasPrecision(9, 2);
            b.HasIndex(x => new { x.OrgId, x.ProjectId, x.Status });
            b.HasIndex(x => new { x.OrgId, x.Number });
        });

        mb.Entity<ProjectTimesheet>(b =>
        {
            b.ToTable("prj_Timesheets");
            b.Property(x => x.WorkerName).HasMaxLength(100).IsRequired();
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.Hours).HasPrecision(9, 2);
            b.Property(x => x.HourlyRate).HasPrecision(19, 4);
            b.Property(x => x.TotalAmount).HasPrecision(19, 4);
            b.HasIndex(x => new { x.OrgId, x.ProjectId, x.IsBilled });
            b.HasIndex(x => new { x.OrgId, x.Date });
        });
    }

    public override void MapEndpoints(RouteGroupBuilder group) => ProjectsEndpoints.Map(group);
}
