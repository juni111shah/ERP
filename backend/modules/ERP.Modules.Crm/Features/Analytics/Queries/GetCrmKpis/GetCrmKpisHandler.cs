using ERP.Application.Common;
using ERP.Modules.Crm.Domain;
using ERP.Modules.Crm.Repositories;

namespace ERP.Modules.Crm.Features;

internal sealed class GetCrmKpisHandler(ICrmLeadRepository leadRepo, ICrmOpportunityRepository oppRepo) : IQueryHandler<GetCrmKpisQuery, CrmKpisDto>
{
    public async Task<CrmKpisDto> Handle(GetCrmKpisQuery request, CancellationToken cancellationToken)
    {
        // 1. The leads and opportunities of the entity (or all).
        var leads = await leadRepo.ListByLegalEntityAsync(request.LegalEntityId, cancellationToken);
        var opps = await oppRepo.ListByLegalEntityAsync(request.LegalEntityId, cancellationToken);

        // 2. Lead figures.
        var totalLeads = leads.Count;
        var qualifiedLeads = leads.Count(x => x.Status == LeadStatus.Qualified || x.Status == LeadStatus.Converted);
        var conversionRate = totalLeads == 0 ? 0 : Math.Round((decimal)leads.Count(x => x.Status == LeadStatus.Converted) / totalLeads * 100, 1);

        // 3. Pipeline figures.
        var openOpps = opps.Where(x => x.Stage != OpportunityStage.ClosedWon && x.Stage != OpportunityStage.ClosedLost).ToList();
        var wonOpps = opps.Where(x => x.Stage == OpportunityStage.ClosedWon).ToList();
        var closedOppsCount = opps.Count(x => x.Stage == OpportunityStage.ClosedWon || x.Stage == OpportunityStage.ClosedLost);

        var winRate = closedOppsCount == 0 ? 0 : Math.Round((decimal)wonOpps.Count / closedOppsCount * 100, 1);

        return new CrmKpisDto(
            totalLeads,
            qualifiedLeads,
            conversionRate,
            openOpps.Count,
            openOpps.Sum(x => x.ExpectedRevenue),
            wonOpps.Count,
            wonOpps.Sum(x => x.ActualRevenue > 0 ? x.ActualRevenue : x.ExpectedRevenue),
            winRate);
    }
}
