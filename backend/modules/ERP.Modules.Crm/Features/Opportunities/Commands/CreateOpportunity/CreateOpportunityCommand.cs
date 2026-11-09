using ERP.Application.Common;
using ERP.Modules.Crm.Domain;

namespace ERP.Modules.Crm.Features;

/// <summary>Creates an open opportunity, numbered from the OPP sequence of its legal entity.</summary>
public record CreateOpportunityCommand(
    Guid LegalEntityId,
    string Title,
    Guid? CustomerPartyId,
    string CustomerName,
    OpportunityStage Stage,
    int ProbabilityPercent,
    decimal ExpectedRevenue,
    DateOnly? ExpectedCloseDate,
    Guid? AssignedUserId,
    string? AssignedUserName,
    string? Notes,
    string? CustomFields) : ICommand<OpportunityDto>;
