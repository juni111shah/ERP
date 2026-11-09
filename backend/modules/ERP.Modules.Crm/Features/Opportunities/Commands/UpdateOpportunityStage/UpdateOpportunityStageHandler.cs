using ERP.Application.Common;
using ERP.Modules.Crm.Domain;
using ERP.Modules.Crm.Repositories;

namespace ERP.Modules.Crm.Features;

internal sealed class UpdateOpportunityStageHandler(ICrmOpportunityRepository opportunities) : ICommandHandler<UpdateOpportunityStageCommand, OpportunityDto>
{
    public async Task<OpportunityDto> Handle(UpdateOpportunityStageCommand request, CancellationToken cancellationToken)
    {
        // 1. Load the opportunity; the move must be an allowed transition.
        var opp = await opportunities.FindForUpdateAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Opportunity {request.Id} not found.");

        CrmTransitions.Opportunity.Ensure(opp.Stage, request.Stage, isSystem: false);

        // 2. Move it; the probability follows a closing stage.
        var wasClosed = opp.Stage is OpportunityStage.ClosedWon or OpportunityStage.ClosedLost;
        opp.Stage = request.Stage;
        opp.ProbabilityPercent = request.Stage switch { OpportunityStage.ClosedWon => 100, OpportunityStage.ClosedLost => 0, _ => request.ProbabilityPercent };

        // 3. Record or clear the close.
        if (request.Stage == OpportunityStage.ClosedWon)
        {
            opp.ActualCloseDate = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
            opp.ActualRevenue = opp.ExpectedRevenue;
        }
        else if (request.Stage == OpportunityStage.ClosedLost)
        {
            opp.ActualCloseDate = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
            opp.ActualRevenue = 0;
        }
        else if (wasClosed)
        {
            // reopened: nothing of the earlier close may linger
            opp.ActualCloseDate = null;
            opp.ActualRevenue = 0;
            opp.CloseReason = null;
        }

        await opportunities.SaveChangesAsync(cancellationToken);
        return OpportunityDto.From(opp);
    }
}
