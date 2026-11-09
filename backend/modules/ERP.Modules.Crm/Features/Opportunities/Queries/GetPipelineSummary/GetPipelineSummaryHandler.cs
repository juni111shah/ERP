using ERP.Application.Common;
using ERP.Modules.Crm.Domain;
using ERP.Modules.Crm.Repositories;

namespace ERP.Modules.Crm.Features;

internal sealed class GetPipelineSummaryHandler(ICrmOpportunityRepository opportunities) : IQueryHandler<GetPipelineSummaryQuery, List<PipelineStageSummaryDto>>
{
    public async Task<List<PipelineStageSummaryDto>> Handle(GetPipelineSummaryQuery request, CancellationToken cancellationToken)
    {
        // 1. The opportunities of the entity (or all).
        var opps = await opportunities.ListByLegalEntityAsync(request.LegalEntityId, cancellationToken);

        // 2. One row per stage, in pipeline order.
        var stages = new[]
        {
            OpportunityStage.Qualification,
            OpportunityStage.Proposal,
            OpportunityStage.Negotiation,
            OpportunityStage.ClosedWon,
            OpportunityStage.ClosedLost
        };

        return stages.Select(s => new PipelineStageSummaryDto(
            s,
            s.ToString(),
            opps.Count(x => x.Stage == s),
            opps.Where(x => x.Stage == s).Sum(x => x.ExpectedRevenue)
        )).ToList();
    }
}
