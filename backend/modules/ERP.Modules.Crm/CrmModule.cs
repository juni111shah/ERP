using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Application.Search;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Modularity;
using ERP.Modules.Crm.Domain;
using ERP.Modules.Crm.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Crm;

public sealed class CrmModule : ModuleBase
{
    public const string Key = "crm";

    public override ModuleManifest Manifest { get; } = new(
        Key,
        "CRM",
        "Lead acquisition, sales deal pipeline, interaction logging, and customer relationship management.",
        "Horizontal");

    public override IEnumerable<PermissionDefinition> Permissions => Crm.Permissions.Definitions;

    public override IEnumerable<MenuItemDefinition> Menu =>
    [
        new("crm.leads", "Leads & contacts", "/crm/leads", "user-add", Crm.Permissions.LeadsRead, 10, Key, "CRM", 220),
        new("crm.opportunities", "Sales pipeline", "/crm/opportunities", "funnel-plot", Crm.Permissions.OpportunitiesRead, 20, Key, "CRM", 220),
        new("crm.interactions", "Activity logs", "/crm/interactions", "phone", Crm.Permissions.InteractionsRead, 30, Key, "CRM", 220),
        new("crm.analytics", "CRM analytics", "/crm/analytics", "line-chart", Crm.Permissions.AnalyticsRead, 40, Key, "CRM", 220)
    ];

    public override IEnumerable<IndustryTemplate> IndustryTemplates =>
    [
        new("crm", "CRM & Sales", "Lead management, sales pipeline, and customer interactions.", IndustryAvailability.Available, [Key],
            Roles: [],
            Sequences:
            [
                new("LEAD", "LEAD", "{prefix}-{seq:5}", false),
                new("OPP", "OPP", "{prefix}-{seq:5}", false),
            ],
            SortOrder: 60),
    ];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISearchProvider, CrmSearch>();
        services.AddScoped<IDashboardContributor, CrmDashboard>();
        services.AddScoped<ERP.Application.Workflow.IAttachmentOwnerResolver>(sp =>
            new ERP.Application.Workflow.EntityAttachmentOwnerResolver<CrmLead>(
                sp.GetRequiredService<ERP.Application.Abstractions.IEntityStore>(), "lead", Crm.Permissions.LeadsRead, Crm.Permissions.LeadsManage));
        services.AddScoped<ERP.Application.Workflow.IAttachmentOwnerResolver>(sp =>
            new ERP.Application.Workflow.EntityAttachmentOwnerResolver<CrmOpportunity>(
                sp.GetRequiredService<ERP.Application.Abstractions.IEntityStore>(), "opportunity", Crm.Permissions.OpportunitiesRead, Crm.Permissions.OpportunitiesManage));
    }

    public override void MapEndpoints(RouteGroupBuilder group) => CrmEndpoints.Map(group);

    public override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CrmLead>(b =>
        {
            b.ToTable("crm_Leads");
            b.HasKey(x => x.Id);
            b.Property(x => x.LeadNumber).HasMaxLength(40).IsRequired();
            b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            b.Property(x => x.CompanyName).HasMaxLength(150);
            b.Property(x => x.Email).HasMaxLength(150);
            b.Property(x => x.Phone).HasMaxLength(50);
            b.Property(x => x.EstimatedValue).HasPrecision(18, 2);
            b.HasIndex(x => new { x.LegalEntityId, x.LeadNumber }).IsUnique();
            b.HasIndex(x => new { x.LegalEntityId, x.Status });
            b.HasIndex(x => new { x.LegalEntityId, x.Email });
        });

        modelBuilder.Entity<CrmOpportunity>(b =>
        {
            b.ToTable("crm_Opportunities");
            b.HasKey(x => x.Id);
            b.Property(x => x.OpportunityNumber).HasMaxLength(40).IsRequired();
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.CustomerName).HasMaxLength(150).IsRequired();
            b.Property(x => x.ExpectedRevenue).HasPrecision(18, 2);
            b.Property(x => x.ActualRevenue).HasPrecision(18, 2);
            b.HasIndex(x => new { x.LegalEntityId, x.OpportunityNumber }).IsUnique();
            b.HasIndex(x => new { x.LegalEntityId, x.Stage });
            b.HasIndex(x => new { x.LegalEntityId, x.ExpectedCloseDate });
        });

        modelBuilder.Entity<CrmInteraction>(b =>
        {
            b.ToTable("crm_Interactions");
            b.HasKey(x => x.Id);
            b.Property(x => x.RelatedEntityKind).HasMaxLength(50).IsRequired();
            b.Property(x => x.Subject).HasMaxLength(200).IsRequired();
            b.HasIndex(x => new { x.LegalEntityId, x.RelatedEntityKind, x.RelatedEntityId });
            b.HasIndex(x => new { x.LegalEntityId, x.InteractionDate });
        });
    }
}
