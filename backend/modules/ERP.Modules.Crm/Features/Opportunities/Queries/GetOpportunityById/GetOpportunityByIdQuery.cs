using ERP.Application.Common;

namespace ERP.Modules.Crm.Features;

/// <summary>One opportunity, or null when it does not exist.</summary>
public record GetOpportunityByIdQuery(Guid Id) : IQuery<OpportunityDto?>;
