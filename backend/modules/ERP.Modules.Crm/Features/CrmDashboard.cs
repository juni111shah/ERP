using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Modules.Crm.Domain;

namespace ERP.Modules.Crm.Features;

public sealed class CrmDashboard(
    IReadRepository<CrmLead> leads,
    IReadRepository<CrmOpportunity> opportunities,
    ICurrentUser user) : IDashboardContributor
{
    public string ModuleKey => CrmModule.Key;

    public async Task<DashboardContribution> BuildAsync(DashboardScope scope, CancellationToken ct)
    {
        var entity = scope.LegalEntityId;
        var kpis = new List<KpiCard>();

        var charts = new List<DashboardChart>();

        if (user.HasPermission(Permissions.LeadsRead))
        {
            var newLeadsCount = await leads.CountAsync(l => l.Status == LeadStatus.New && (entity == null || l.LegalEntityId == entity), ct);
            kpis.Add(new("crm-new-leads", "New Leads", newLeadsCount, "number", "inbound prospects", 180, "/crm/leads"));
        }

        if (user.HasPermission(Permissions.OpportunitiesRead))
        {
            var activeOpps = await opportunities.CountAsync(o => o.Stage != OpportunityStage.ClosedWon && o.Stage != OpportunityStage.ClosedLost && (entity == null || o.LegalEntityId == entity), ct);
            kpis.Add(new("crm-active-deals", "Active Deals", activeOpps, "number", "pipeline opportunities", 185, "/crm/opportunities"));

            var stages = new[] { OpportunityStage.Qualification, OpportunityStage.Proposal, OpportunityStage.Negotiation, OpportunityStage.ClosedWon, OpportunityStage.ClosedLost };
            var counts = new List<decimal>();
            foreach (var st in stages)
            {
                var c = await opportunities.CountAsync(o => o.Stage == st && (entity == null || o.LegalEntityId == entity), ct);
                counts.Add((decimal)c);
            }
            charts.Add(new("crm-funnel", "CRM opportunity pipeline", "bar",
                ["Qualification", "Proposal", "Negotiation", "Won", "Lost"],
                [new("Opportunities", counts)], "Deals by pipeline stage", 35));
        }

        return new DashboardContribution(kpis, charts, []);
    }
}
