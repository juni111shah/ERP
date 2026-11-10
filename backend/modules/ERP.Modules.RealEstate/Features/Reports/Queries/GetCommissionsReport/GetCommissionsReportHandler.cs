using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetCommissionsReportHandler(IBrokerageDealRepository deals, IRealEstatePartyRepository parties) : IQueryHandler<GetCommissionsReportQuery, CommissionsDto>
{
    public async Task<CommissionsDto> Handle(GetCommissionsReportQuery q, CancellationToken ct)
    {
        // 1. The deals in the period that are not lost, and the agents' names.
        var entity = q.LegalEntityId;
        var rows = await deals.ListCommissionRowsAsync(q.From, q.To, entity, ct);
        var agentIds = rows.Where(r => r.AgentPartyId != null).Select(r => r.AgentPartyId!.Value).Distinct().ToList();
        var names = await parties.NamesAsync(agentIds, ct);

        // 2. One row per agent, then the total.
        string Agent(Guid? id, string? external) => id is { } k ? names.GetValueOrDefault(k, "?") : external ?? "Unassigned";
        decimal Share(decimal gross, decimal pct) => Math.Round(gross * pct / 100m, 2, MidpointRounding.AwayFromZero);
        var result = rows.GroupBy(r => Agent(r.AgentPartyId, r.ExternalAgentName)).Select(g =>
        {
            var agent = g.Sum(r => Share(r.GrossCommission, r.AgentSharePercent)); var lead = g.Sum(r => Share(r.GrossCommission, r.TeamLeadSharePercent)); var gross = g.Sum(r => r.GrossCommission);
            return new CommissionRow(g.Key, g.Count(), g.Sum(r => r.DealValue), gross, agent, lead, gross - agent - lead, g.Where(r => r.Stage == DealStage.AgentPaid).Sum(r => Share(r.GrossCommission, r.AgentSharePercent)));
        }).OrderBy(r => r.Agent).ToList();
        var total = new CommissionRow("Total", result.Sum(r => r.Deals), result.Sum(r => r.DealValue), result.Sum(r => r.GrossCommission), result.Sum(r => r.AgentShare), result.Sum(r => r.TeamLeadShare), result.Sum(r => r.CompanyShare), result.Sum(r => r.PaidOut));
        return new CommissionsDto(q.From, q.To, result, total);
    }
}
