using ERP.Application.Common;
using ERP.Modules.Crm.Domain;

namespace ERP.Modules.Crm.Features;

/// <summary>Logs a call, email, meeting or note against a lead, opportunity or customer.</summary>
public record LogInteractionCommand(
    Guid LegalEntityId,
    string RelatedEntityKind,
    Guid RelatedEntityId,
    InteractionType Type,
    string Subject,
    string? Details,
    DateTimeOffset? InteractionDate,
    Guid? PerformedByUserId,
    string? PerformedByName,
    DateTimeOffset? FollowUpDate,
    bool IsResolved,
    string? CustomFields) : ICommand<CustomerInteractionDto>;
