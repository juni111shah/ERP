using ERP.Modules.Crm.Domain;

namespace ERP.Modules.Crm.Features;

public record LeadDto(
    Guid Id,
    Guid LegalEntityId,
    string LeadNumber,
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
    Guid? ConvertedPartyId,
    Guid? ConvertedOpportunityId,
    DateTimeOffset? ConvertedAt,
    string? CustomFields,
    DateTimeOffset CreatedAt)
{
    public static LeadDto From(CrmLead lead) => new(
        lead.Id,
        lead.LegalEntityId,
        lead.LeadNumber,
        lead.FullName,
        lead.CompanyName,
        lead.JobTitle,
        lead.Email,
        lead.Phone,
        lead.Source,
        lead.Status,
        lead.EstimatedValue,
        lead.AssignedUserId,
        lead.AssignedUserName,
        lead.Notes,
        lead.ConvertedPartyId,
        lead.ConvertedOpportunityId,
        lead.ConvertedAt,
        lead.CustomFields,
        lead.CreatedAt);
}
