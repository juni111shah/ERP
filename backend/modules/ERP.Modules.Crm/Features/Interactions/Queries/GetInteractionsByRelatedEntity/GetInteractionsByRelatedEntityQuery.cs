using ERP.Application.Common;

namespace ERP.Modules.Crm.Features;

/// <summary>The interactions logged against one lead, opportunity or customer, latest first.</summary>
public record GetInteractionsByRelatedEntityQuery(
    string RelatedEntityKind,
    Guid RelatedEntityId) : IQuery<List<CustomerInteractionDto>>;
