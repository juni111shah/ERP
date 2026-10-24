using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Crm.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Crm.Features;

public record CrmKpisDto(
    int TotalLeads,
    int QualifiedLeads,
    decimal LeadConversionRatePercentage,
    int OpenOpportunitiesCount,
    decimal OpenPipelineValue,
    int WonOpportunitiesCount,
    decimal WonOpportunitiesRevenue,
    decimal WinRatePercentage);

public record GetCrmKpisQuery(Guid? LegalEntityId = null) : IQuery<CrmKpisDto>;

public class CrmKpiHandlers : IRequestHandler<GetCrmKpisQuery, CrmKpisDto>
{
    private readonly IRepository<CrmLead> _leadRepo;
    private readonly IRepository<CrmOpportunity> _oppRepo;

    public CrmKpiHandlers(
        IRepository<CrmLead> leadRepo,
        IRepository<CrmOpportunity> oppRepo)
    {
        _leadRepo = leadRepo;
        _oppRepo = oppRepo;
    }

    public async Task<CrmKpisDto> Handle(GetCrmKpisQuery request, CancellationToken cancellationToken)
    {
        var leadQuery = _leadRepo.Query();
        var oppQuery = _oppRepo.Query();

        if (request.LegalEntityId.HasValue)
        {
            leadQuery = leadQuery.Where(x => x.LegalEntityId == request.LegalEntityId.Value);
            oppQuery = oppQuery.Where(x => x.LegalEntityId == request.LegalEntityId.Value);
        }

        var leads = await leadQuery.ToListAsync(cancellationToken);
        var opps = await oppQuery.ToListAsync(cancellationToken);

        var totalLeads = leads.Count;
        var qualifiedLeads = leads.Count(x => x.Status == LeadStatus.Qualified || x.Status == LeadStatus.Converted);
        var conversionRate = totalLeads == 0 ? 0 : Math.Round((decimal)leads.Count(x => x.Status == LeadStatus.Converted) / totalLeads * 100, 1);

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
