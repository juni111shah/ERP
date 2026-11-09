using ERP.Application.Common;
using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Features;

/// <summary>Opens a client engagement; the client name comes from the customer record.</summary>
public record CreateEngagementCommand(
    Guid LegalEntityId,
    string Title,
    Guid ClientId,
    string ClientName,
    EngagementType Type,
    decimal ContractValue,
    decimal BudgetedHours,
    DateOnly StartDate,
    DateOnly? TargetEndDate,
    Guid? LeadConsultantUserId,
    string? LeadConsultantName,
    string? Description,
    string? CustomFields) : ICommand<EngagementDto>;
