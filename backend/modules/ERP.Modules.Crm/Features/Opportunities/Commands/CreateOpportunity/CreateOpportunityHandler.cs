using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Crm.Domain;
using ERP.Modules.Crm.Repositories;

namespace ERP.Modules.Crm.Features;

internal sealed class CreateOpportunityHandler(
    ICrmOpportunityRepository opportunities,
    INumberingService numbering,
    ILegalEntityGuard guard,
    IReadRepository<ERP.Domain.Parties.Party> parties) : ICommandHandler<CreateOpportunityCommand, OpportunityDto>
{
    public async Task<OpportunityDto> Handle(CreateOpportunityCommand request, CancellationToken cancellationToken)
    {
        // 1. Checks: entity access, an open stage, and a customer that exists.
        await guard.RequireAsync(request.LegalEntityId, cancellationToken);
        if (request.Stage is OpportunityStage.ClosedWon or OpportunityStage.ClosedLost)
            throw new FieldValidationException("stage", "A new opportunity starts open. Close it with the close action.");
        if (request.CustomerPartyId is { } partyId && !await parties.AnyAsync(p => p.Id == partyId, cancellationToken))
            throw new FieldValidationException("customerPartyId", "Customer not found.");
        var number = await numbering.NextAsync("OPP", request.LegalEntityId, DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), cancellationToken);

        // 2. Create and save.
        var opp = new CrmOpportunity
        {
            LegalEntityId = request.LegalEntityId,
            OpportunityNumber = number,
            Title = request.Title.Trim(),
            CustomerPartyId = request.CustomerPartyId,
            CustomerName = request.CustomerName.Trim(),
            Stage = request.Stage,
            ProbabilityPercent = request.ProbabilityPercent,
            ExpectedRevenue = request.ExpectedRevenue,
            ExpectedCloseDate = request.ExpectedCloseDate,
            AssignedUserId = request.AssignedUserId,
            AssignedUserName = request.AssignedUserName,
            Notes = request.Notes,
            CustomFields = request.CustomFields
        };

        opportunities.Add(opp);
        await opportunities.SaveChangesAsync(cancellationToken);
        return OpportunityDto.From(opp);
    }
}
