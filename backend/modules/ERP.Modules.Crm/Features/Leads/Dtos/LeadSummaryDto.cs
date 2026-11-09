using System.Linq.Expressions;
using ERP.Modules.Crm.Domain;

namespace ERP.Modules.Crm.Features;

public record LeadSummaryDto(
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
    string? AssignedUserName,
    string? Notes,
    DateTimeOffset CreatedAt)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<CrmLead, LeadSummaryDto>> Projection = l => new LeadSummaryDto(
        l.Id,
        l.LegalEntityId,
        l.LeadNumber,
        l.FullName,
        l.CompanyName,
        l.JobTitle,
        l.Email,
        l.Phone,
        l.Source,
        l.Status,
        l.EstimatedValue,
        l.AssignedUserName,
        l.Notes,
        l.CreatedAt);
}
