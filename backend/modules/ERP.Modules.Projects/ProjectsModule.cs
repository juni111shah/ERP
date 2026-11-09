using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Application.Dashboard;
using ERP.Application.Search;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Modularity;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Features;
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
        PermissionDefinition.Of(P.TimesheetsApprove, "Approve or reject submitted timesheets"),
        PermissionDefinition.Of(P.ExpensesRead, "View project expenses"),
        PermissionDefinition.Of(P.ExpensesManage, "Record and edit project expenses"),
        PermissionDefinition.Of(P.ExpensesApprove, "Approve or reject project expenses"),
        PermissionDefinition.Of(P.ReportsRead, "View project reports (profitability, utilisation, budget)"),
        PermissionDefinition.Of(P.ChangeOrdersRead, "View change orders"),
        PermissionDefinition.Of(P.ChangeOrdersManage, "Create and decide change orders"),
        PermissionDefinition.Of(P.RisksRead, "View risks and issues"),
        PermissionDefinition.Of(P.RisksManage, "Log and update risks and issues"),
        PermissionDefinition.Of(P.RatesRead, "View rate cards"),
        PermissionDefinition.Of(P.RatesManage, "Manage rate cards"),
        PermissionDefinition.Of(P.TeamManage, "Manage project teams and allocations"),
    ];

    public override IEnumerable<MenuItemDefinition> Menu =>
    [
        new("projects.projects", "Projects", "/projects/list", "project", P.ProjectsRead, 10, "projects", "Projects", 350),
        new("projects.tasks", "Tasks", "/projects/tasks", "check-square", P.TasksRead, 20, "projects", "Projects", 350),
        new("projects.board", "Task board", "/projects/board", "table", P.TasksRead, 25, "projects", "Projects", 350),
        new("projects.risks", "Risks and issues", "/projects/risks", "warning", P.RisksRead, 30, "projects", "Projects", 350),
        new("projects.changeorders", "Change orders", "/projects/change-orders", "swap", P.ChangeOrdersRead, 35, "projects", "Projects", 350),
        new("projects.resources", "Resources", "/projects/resources", "team", P.TeamManage, 40, "projects", "Projects", 350),
        new("projects.week", "My week", "/projects/week", "calendar", P.TimesheetsManage, 10, "projects.money", "Project time and money", 351),
        new("projects.timesheets", "Timesheets", "/projects/timesheets", "clock-circle", P.TimesheetsRead, 15, "projects.money", "Project time and money", 351),
        new("projects.expenses", "Expenses", "/projects/expenses", "wallet", P.ExpensesRead, 20, "projects.money", "Project time and money", 351),
        new("projects.approvals", "Time approvals", "/projects/approvals?tab=time", "audit", P.TimesheetsApprove, 25, "projects.money", "Project time and money", 351),
        new("projects.approvals.expenses", "Expense approvals", "/projects/approvals?tab=expenses", "file-done", P.ExpensesApprove, 26, "projects.money", "Project time and money", 351),
        new("projects.billing", "Project billing", "/projects/billing", "dollar-circle", P.BillingRead, 30, "projects.money", "Project time and money", 351),
        new("projects.reports", "Project reports", "/projects/reports", "bar-chart", P.ReportsRead, 35, "projects.money", "Project time and money", 351),
    ];

    public override IEnumerable<SettingDefinition> Settings =>
    [
        new("projects.defaultHourlyRate", "Default billable rate per hour", "150", "number"),
        new("projects.requireMilestoneApproval", "Milestones require approval before billing", "false", "bool"),
        new("projects.timesheetApproval", "Timesheets need a manager's approval before they can be billed", "false", "bool", "When on, entries start as drafts, are submitted by the worker and approved by someone with the approve permission."),
        new("projects.expenseApproval", "Expenses need approval before they can be billed", "false", "bool"),
        new("projects.maxHoursPerDay", "Most hours one person can log in a day", "16", "number"),
        new("projects.weeklyCapacityHours", "Working hours in a week (capacity for utilisation)", "40", "number"),
        new("projects.restrictToMembers", "People without manage rights only reach projects they manage or belong to", "false", "bool", "Applies to opening a project and adding tasks, time and expenses to it."),
        new("projects.allowFutureTime", "Allow time to be logged for future dates", "false", "bool"),
        new("projects.allowSelfApproval", "People may approve their own time and expenses", "false", "bool", "Leave off so a second person checks the work. Turn on for very small teams."),
        new("projects.workWeekDays", "Working days of the week", "\"Mon,Tue,Wed,Thu,Fri\"", "text", "Comma-separated, for capacity and scheduling: for example Sun,Mon,Tue,Wed,Thu."),
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
                     P.MilestonesRead, P.MilestonesManage, P.BillingRead, P.BillingManage, P.TimesheetsApprove, P.ExpensesRead, P.ExpensesManage, P.ExpensesApprove,
                     P.ReportsRead, P.ChangeOrdersRead, P.ChangeOrdersManage, P.RisksRead, P.RisksManage, P.RatesRead, P.RatesManage, P.TeamManage, CorePermissions.ReportsRead], Key),
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
        services.AddScoped<ProjectPeople>();
        services.AddScoped<RateResolver>();
        services.AddScoped<ProjectAccess>();
        services.AddScoped<ProjectFigures>();
        services.AddScoped<ProjectDtoBuilder>();
        services.AddScoped<ProjectChecks>();
        services.AddScoped<ProjectScheduleBuilder>();
        services.AddScoped<TaskRules>();
        services.AddScoped<TaskDtoBuilder>();
        services.AddScoped<MilestoneRules>();
        services.AddScoped<MilestoneDtoBuilder>();
        services.AddScoped<TimesheetEntries>();
        services.AddScoped<TimesheetDtoBuilder>();
        services.AddScoped<ExpenseRules>();
        services.AddScoped<ExpenseDtoBuilder>();
        services.AddScoped<BillingPlanner>();
        services.AddScoped<IDashboardContributor, ProjectsDashboard>();
        services.AddScoped<IOwnerDashboardSection, ProjectsOwnerSection>();
        services.AddScoped<ISearchProvider, ProjectsSearch>();
        services.AddScoped<ERP.Application.Reminders.IReminderSource, ProjectsReminders>();
        services.AddScoped<ERP.Application.Imports.IImportDefinition, ProjectTaskImport>();
        foreach (var (owner, read, manage) in new[] { ("projecttask", P.TasksRead, P.TasksManage), ("projectexpense", P.ExpensesRead, P.ExpensesManage) })
        {
            var (o, r, m) = (owner, read, manage);
            if (o == "projecttask") services.AddScoped<ERP.Application.Workflow.IAttachmentOwnerResolver>(sp => new ERP.Application.Workflow.EntityAttachmentOwnerResolver<ProjectTask>(sp.GetRequiredService<ERP.Application.Abstractions.IEntityStore>(), o, r, m));
            else services.AddScoped<ERP.Application.Workflow.IAttachmentOwnerResolver>(sp => new ERP.Application.Workflow.EntityAttachmentOwnerResolver<ProjectExpense>(sp.GetRequiredService<ERP.Application.Abstractions.IEntityStore>(), o, r, m));
        }
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
            b.Property(x => x.ContractValue).HasPrecision(19, 4);
            b.Property(x => x.BudgetHours).HasPrecision(9, 2);
            b.Property(x => x.SourceType).HasMaxLength(60);
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
            b.Property(x => x.Labels).HasMaxLength(300);
            b.HasIndex(x => new { x.OrgId, x.AssignedUserId });
            b.HasIndex(x => new { x.OrgId, x.ParentTaskId });
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
            b.Property(x => x.CostRate).HasPrecision(19, 4);
            b.Property(x => x.CostAmount).HasPrecision(19, 4);
            b.Property(x => x.RejectReason).HasMaxLength(300);
            b.HasIndex(x => new { x.OrgId, x.WorkerUserId, x.Date });
            b.HasIndex(x => new { x.OrgId, x.ProjectId, x.IsBilled });
            b.HasIndex(x => new { x.OrgId, x.Date });
        });
        mb.Entity<ProjectRate>(b =>
        {
            b.ToTable("prj_Rates");
            b.Property(x => x.Role).HasMaxLength(100);
            b.Property(x => x.BillRate).HasPrecision(19, 4);
            b.Property(x => x.CostRate).HasPrecision(19, 4);
            b.HasIndex(x => new { x.OrgId, x.ProjectId, x.UserId });
        });
        mb.Entity<ProjectExpense>(b =>
        {
            b.ToTable("prj_Expenses");
            b.Property(x => x.Category).HasMaxLength(60).IsRequired();
            b.Property(x => x.Description).HasMaxLength(500).IsRequired();
            b.Property(x => x.WorkerName).HasMaxLength(100);
            b.Property(x => x.RejectReason).HasMaxLength(300);
            b.Property(x => x.Amount).HasPrecision(19, 4);
            b.Property(x => x.MarkupPercent).HasPrecision(9, 2);
            b.HasIndex(x => new { x.OrgId, x.ProjectId, x.IsBilled });
        });
        mb.Entity<ProjectTaskDependency>(b =>
        {
            b.ToTable("prj_TaskDependencies");
            b.HasIndex(x => new { x.OrgId, x.TaskId, x.DependsOnTaskId }).IsUnique().HasFilter("[IsDeleted] = 0");
        });
        mb.Entity<ProjectComment>(b =>
        {
            b.ToTable("prj_Comments");
            b.Property(x => x.OwnerType).HasMaxLength(20).IsRequired();
            b.Property(x => x.AuthorName).HasMaxLength(150);
            b.Property(x => x.Text).HasMaxLength(2000).IsRequired();
            b.HasIndex(x => new { x.OrgId, x.OwnerType, x.OwnerId });
        });
        mb.Entity<ProjectChangeOrder>(b =>
        {
            b.ToTable("prj_ChangeOrders");
            b.Property(x => x.Number).HasMaxLength(30).IsRequired();
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.DecisionNote).HasMaxLength(500);
            b.Property(x => x.AmountChange).HasPrecision(19, 4);
            b.Property(x => x.HoursChange).HasPrecision(9, 2);
            b.HasIndex(x => new { x.OrgId, x.ProjectId });
        });
        mb.Entity<ProjectRisk>(b =>
        {
            b.ToTable("prj_Risks");
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.OwnerName).HasMaxLength(150);
            b.HasIndex(x => new { x.OrgId, x.ProjectId, x.Status });
        });
        mb.Entity<ProjectMember>(b =>
        {
            b.ToTable("prj_Members");
            b.Property(x => x.Name).HasMaxLength(150);
            b.Property(x => x.Role).HasMaxLength(100);
            b.HasIndex(x => new { x.OrgId, x.ProjectId, x.UserId }).IsUnique().HasFilter("[IsDeleted] = 0");
        });
        mb.Entity<ProjectAllocation>(b =>
        {
            b.ToTable("prj_Allocations");
            b.Property(x => x.UserName).HasMaxLength(150);
            b.Property(x => x.HoursPerWeek).HasPrecision(9, 2);
            b.HasIndex(x => new { x.OrgId, x.UserId, x.FromDate });
        });
        mb.Entity<ProjectAcceptance>(b =>
        {
            b.ToTable("prj_Acceptances");
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.RequestedByName).HasMaxLength(150);
            b.Property(x => x.SignedByName).HasMaxLength(150);
            b.Property(x => x.Signature).HasMaxLength(150);
            b.Property(x => x.SignedIp).HasMaxLength(64);
            b.Property(x => x.EvidenceHash).HasMaxLength(64);
            b.Property(x => x.SignatureImage).HasMaxLength(300_000);   // a drawn signature is a PNG data URL; over 4000 becomes nvarchar(max)
            b.Property(x => x.Comment).HasMaxLength(1000);
            b.HasIndex(x => new { x.OrgId, x.ProjectId, x.Status });
        });
        mb.Entity<ProjectBudgetLine>(b =>
        {
            b.ToTable("prj_BudgetLines");
            b.Property(x => x.Category).HasMaxLength(60).IsRequired();
            b.Property(x => x.Amount).HasPrecision(19, 4);
            b.Property(x => x.Hours).HasPrecision(9, 2);
            b.HasIndex(x => new { x.OrgId, x.ProjectId, x.Category }).IsUnique().HasFilter("[IsDeleted] = 0");
        });
        mb.Entity<ProjectProgressBilling>(b =>
        {
            b.ToTable("prj_ProgressBillings");
            b.Property(x => x.Percent).HasPrecision(9, 4);
            b.Property(x => x.Amount).HasPrecision(19, 4);
            b.HasIndex(x => new { x.OrgId, x.ProjectId });
        });
    }
}
