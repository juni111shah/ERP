using ERP.Application.Common;

namespace ERP.Modules.Crm.Features;

/// <summary>One lead, or null when it does not exist.</summary>
public record GetLeadByIdQuery(Guid Id) : IQuery<LeadDto?>;
