using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Crm.Domain;
using ERP.Modules.Crm.Repositories;

namespace ERP.Modules.Crm.Features;

internal sealed class LogInteractionHandler(
    ICrmInteractionRepository interactions,
    IReadRepository<CrmLead> leads,
    IReadRepository<CrmOpportunity> opportunities,
    IReadRepository<ERP.Domain.Parties.Party> parties,
    ILegalEntityGuard guard,
    ICurrentUser user) : ICommandHandler<LogInteractionCommand, CustomerInteractionDto>
{
    public async Task<CustomerInteractionDto> Handle(LogInteractionCommand request, CancellationToken cancellationToken)
    {
        // 1. The caller must have access to the entity, and the related record must exist in it.
        await guard.RequireAsync(request.LegalEntityId, cancellationToken);
        var kind = InteractionKinds.All.First(k => string.Equals(k, request.RelatedEntityKind.Trim(), StringComparison.OrdinalIgnoreCase));
        var id = request.RelatedEntityId;
        var found = kind switch
        {
            "Lead" => await leads.AnyAsync(l => l.Id == id && l.LegalEntityId == request.LegalEntityId, cancellationToken),
            "Opportunity" => await opportunities.AnyAsync(o => o.Id == id && o.LegalEntityId == request.LegalEntityId, cancellationToken),
            _ => await parties.AnyAsync(p => p.Id == id, cancellationToken),
        };
        if (!found) throw new FieldValidationException("relatedEntityId", $"{kind} not found.");

        // 2. Log it as performed by the caller and save.
        var interaction = new CrmInteraction
        {
            LegalEntityId = request.LegalEntityId,
            RelatedEntityKind = kind,
            RelatedEntityId = request.RelatedEntityId,
            Type = request.Type,
            Subject = request.Subject.Trim(),
            Details = request.Details,
            InteractionDate = request.InteractionDate ?? DateTimeOffset.UtcNow,
            PerformedByUserId = user.UserId, // the caller, never a value sent in the request
            PerformedByName = user.Email,
            FollowUpDate = request.FollowUpDate,
            IsResolved = request.IsResolved,
            CustomFields = request.CustomFields
        };

        interactions.Add(interaction);
        await interactions.SaveChangesAsync(cancellationToken);
        return CustomerInteractionDto.From(interaction);
    }
}
