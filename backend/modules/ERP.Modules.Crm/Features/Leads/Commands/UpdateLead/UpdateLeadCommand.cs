using ERP.Application.Common;
using ERP.Modules.Crm.Domain;

namespace ERP.Modules.Crm.Features;

/// <summary>Edits a lead that is not converted yet.</summary>
public record UpdateLeadCommand(
    Guid Id,
    string FullName,
    string? CompanyName,
    string? JobTitle,
    string? Email,
    string? Phone,
    LeadSource Source,
    LeadStatus Status,
    decimal EstimatedValue,
    Guid? AssignedUserId,
    string? AssignedUserName,
    string? Notes,
    string? CustomFields) : ICommand<LeadDto>;
