using System.Linq.Expressions;
using ERP.Modules.Crm.Domain;

namespace ERP.Modules.Crm.Features;

public record CustomerInteractionDto(
    Guid Id,
    Guid LegalEntityId,
    string RelatedEntityKind,
    Guid RelatedEntityId,
    InteractionType Type,
    string Subject,
    string? Details,
    DateTimeOffset InteractionDate,
    Guid? PerformedByUserId,
    string? PerformedByName,
    DateTimeOffset? FollowUpDate,
    bool IsResolved,
    string? CustomFields,
    DateTimeOffset CreatedAt)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<CrmInteraction, CustomerInteractionDto>> Projection = i => new CustomerInteractionDto(
        i.Id,
        i.LegalEntityId,
        i.RelatedEntityKind,
        i.RelatedEntityId,
        i.Type,
        i.Subject,
        i.Details,
        i.InteractionDate,
        i.PerformedByUserId,
        i.PerformedByName,
        i.FollowUpDate,
        i.IsResolved,
        i.CustomFields,
        i.CreatedAt);

    public static CustomerInteractionDto From(CrmInteraction i) => new(
        i.Id,
        i.LegalEntityId,
        i.RelatedEntityKind,
        i.RelatedEntityId,
        i.Type,
        i.Subject,
        i.Details,
        i.InteractionDate,
        i.PerformedByUserId,
        i.PerformedByName,
        i.FollowUpDate,
        i.IsResolved,
        i.CustomFields,
        i.CreatedAt);
}
