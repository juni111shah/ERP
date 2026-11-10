using ERP.Application.Common;
using ERP.Modules.Crm.Domain;

namespace ERP.Modules.Crm.Features;

/// <summary>Creates a new lead, numbered from the LEAD sequence of its legal entity.</summary>
public record CreateLeadCommand(
    Guid LegalEntityId,
    string FullName,
    string? CompanyName,
    string? JobTitle,
    string? Email,
    string? Phone,
    LeadSource Source,
    decimal EstimatedValue,
    Guid? AssignedUserId,
    string? AssignedUserName,
    string? Notes,
    string? CustomFields) : ICommand<LeadDto>;
