using ERP.Application.Common;
using ERP.Modules.Crm.Domain;

namespace ERP.Modules.Crm.Features;

public static class CrmTransitions
{
    public static readonly StatusTransitions<OpportunityStage> Opportunity = new(
        new Dictionary<OpportunityStage, IEnumerable<OpportunityStage>>
        {
            [OpportunityStage.Qualification] = [OpportunityStage.Proposal, OpportunityStage.ClosedLost],
            [OpportunityStage.Proposal] = [OpportunityStage.Negotiation, OpportunityStage.ClosedLost, OpportunityStage.Qualification],
            [OpportunityStage.Negotiation] = [OpportunityStage.ClosedWon, OpportunityStage.ClosedLost, OpportunityStage.Proposal],
            [OpportunityStage.ClosedWon] = [],
            [OpportunityStage.ClosedLost] = [OpportunityStage.Qualification],
        });
}
