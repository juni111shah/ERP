using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Application.Search;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modules.Services.Domain;
using ERP.Modules.Services.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Services;

public sealed class ServicesModule : ModuleBase
{
    public const string Key = "services";

    public override ModuleManifest Manifest { get; } = new(
        Key,
        "Professional services",
        "Engagements, retainers, milestone deliverables, and time billing.",
        "Industry");

    public override IEnumerable<PermissionDefinition> Permissions => Services.Permissions.Definitions;

    public override IEnumerable<MenuItemDefinition> Menu =>
    [
        new("services.engagements", "Engagements", "/services/engagements", "project", Services.Permissions.EngagementsRead, 10, Key, "Services", 230),
        new("services.retainers", "Retainers", "/services/retainers", "sync", Services.Permissions.RetainersRead, 20, Key, "Services", 230),
        new("services.milestones", "Milestone billing", "/services/milestones", "flag", Services.Permissions.MilestonesRead, 30, Key, "Services", 230),
        new("services.analytics", "Services analytics", "/services/analytics", "line-chart", Services.Permissions.AnalyticsRead, 40, Key, "Services", 230)
    ];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISearchProvider, ServicesSearch>();
        services.AddScoped<IDashboardContributor, ServicesDashboard>();
        services.AddScoped<ERP.Application.Workflow.IAttachmentOwnerResolver>(sp =>
            new ERP.Application.Workflow.EntityAttachmentOwnerResolver<ServiceEngagement>(
                sp.GetRequiredService<ERP.Application.Abstractions.IEntityStore>(), "engagement", Services.Permissions.EngagementsRead, Services.Permissions.EngagementsManage));
    }

    public override void MapEndpoints(RouteGroupBuilder group) => ServicesEndpoints.Map(group);

    public override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServiceEngagement>(b =>
        {
            b.ToTable("svc_Engagements");
            b.HasKey(x => x.Id);
            b.Property(x => x.EngagementCode).HasMaxLength(40).IsRequired();
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.ClientName).HasMaxLength(200).IsRequired();
            b.Property(x => x.ContractValue).HasPrecision(18, 2);
            b.Property(x => x.BudgetedHours).HasPrecision(18, 2);
            b.Property(x => x.ConsumedHours).HasPrecision(18, 2);
            b.Property(x => x.ConsumedCost).HasPrecision(18, 2);
            b.HasIndex(x => new { x.LegalEntityId, x.EngagementCode }).IsUnique();
            b.HasIndex(x => new { x.LegalEntityId, x.Status });
            b.HasIndex(x => new { x.LegalEntityId, x.ClientId });
        });

        modelBuilder.Entity<RetainerContract>(b =>
        {
            b.ToTable("svc_Retainers");
            b.HasKey(x => x.Id);
            b.Property(x => x.RetainerNumber).HasMaxLength(40).IsRequired();
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.ClientName).HasMaxLength(200).IsRequired();
            b.Property(x => x.RecurringFee).HasPrecision(18, 2);
            b.Property(x => x.IncludedHours).HasPrecision(18, 2);
            b.Property(x => x.OverageHourlyRate).HasPrecision(18, 2);
            b.HasIndex(x => new { x.LegalEntityId, x.RetainerNumber }).IsUnique();
            b.HasIndex(x => new { x.LegalEntityId, x.Status });
            b.HasIndex(x => new { x.LegalEntityId, x.EngagementId });
        });

        modelBuilder.Entity<BillingMilestone>(b =>
        {
            b.ToTable("svc_Milestones");
            b.HasKey(x => x.Id);
            b.Property(x => x.MilestoneCode).HasMaxLength(40).IsRequired();
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.HasIndex(x => new { x.LegalEntityId, x.MilestoneCode }).IsUnique();
            b.HasIndex(x => new { x.LegalEntityId, x.Status });
            b.HasIndex(x => new { x.LegalEntityId, x.EngagementId });
        });

        modelBuilder.Entity<TimeEntryRecord>(b =>
        {
            b.ToTable("svc_TimeEntries");
            b.HasKey(x => x.Id);
            b.Property(x => x.ConsultantName).HasMaxLength(150).IsRequired();
            b.Property(x => x.Hours).HasPrecision(18, 2);
            b.Property(x => x.HourlyRate).HasPrecision(18, 2);
            b.Property(x => x.TotalAmount).HasPrecision(18, 2);
            b.Property(x => x.TaskDescription).HasMaxLength(500).IsRequired();
            b.HasIndex(x => new { x.LegalEntityId, x.EngagementId });
            b.HasIndex(x => new { x.LegalEntityId, x.ConsultantUserId });
            b.HasIndex(x => new { x.LegalEntityId, x.ActivityDate });
        });
    }
}
