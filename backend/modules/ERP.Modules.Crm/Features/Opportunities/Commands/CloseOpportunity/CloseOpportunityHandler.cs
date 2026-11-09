using ERP.Application.Common;
using ERP.Modules.Crm.Domain;
using ERP.Modules.Crm.Repositories;

namespace ERP.Modules.Crm.Features;

internal sealed class CloseOpportunityHandler(ICrmOpportunityRepository opportunities) : ICommandHandler<CloseOpportunityCommand, OpportunityDto>
{
    public async Task<OpportunityDto> Handle(CloseOpportunityCommand request, CancellationToken cancellationToken)
    {
        // 1. Load the opportunity; it must still be open.
        var opp = await opportunities.FindForUpdateAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Opportunity {request.Id} not found.");

        if (opp.Stage == OpportunityStage.ClosedWon || opp.Stage == OpportunityStage.ClosedLost)
        {
            throw new ConflictException("Opportunity is already closed.");
        }

        // 2. Close it and save.
        var target = request.IsWon ? OpportunityStage.ClosedWon : OpportunityStage.ClosedLost;
        CrmTransitions.Opportunity.Ensure(opp.Stage, target, isSystem: false); // a deal is won only from Negotiation
        opp.Stage = target;
        opp.ActualRevenue = request.IsWon ? request.ActualRevenue : 0;
        opp.ActualCloseDate = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        opp.CloseReason = request.CloseReason;
        opp.ProbabilityPercent = request.IsWon ? 100 : 0;

        await opportunities.SaveChangesAsync(cancellationToken);
        return OpportunityDto.From(opp);
    }
}
