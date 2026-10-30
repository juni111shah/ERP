using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Features;

public sealed class ServicesDashboard(
    IReadRepository<ServiceEngagement> engagements,
    IReadRepository<BillingMilestone> milestones,
    ICurrentUser user) : IDashboardContributor
{
    public string ModuleKey => ServicesModule.Key;

    public async Task<DashboardContribution> BuildAsync(DashboardScope scope, CancellationToken ct)
    {
        var entity = scope.LegalEntityId;
        var kpis = new List<KpiCard>();

        if (user.HasPermission(Permissions.EngagementsRead))
        {
            var activeEngagements = await engagements.CountAsync(e => e.Status == EngagementStatus.Active && (entity == null || e.LegalEntityId == entity), ct);
            kpis.Add(new("svc-active-engagements", "Active Engagements", activeEngagements, "number", "client contracts", 190, "/services/engagements"));
        }

        if (user.HasPermission(Permissions.MilestonesRead))
        {
            var readyMilestones = await milestones.CountAsync(m => m.Status == MilestoneStatus.ReadyToInvoice && (entity == null || m.LegalEntityId == entity), ct);
            kpis.Add(new("svc-ready-milestones", "Ready to Invoice", readyMilestones, "number", "billable milestones", 195, "/services/milestones"));
        }

        return new DashboardContribution(kpis, [], []);
    }
}
