using System.Linq.Expressions;
using ERP.Modules.Crm.Domain;

namespace ERP.Modules.Crm.Features;

public record OpportunitySummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string OpportunityNumber,
    string Title,
    string CustomerName,
    OpportunityStage Stage,
    int ProbabilityPercent,
    decimal ExpectedRevenue,
    DateOnly? ExpectedCloseDate,
    string? AssignedUserName,
    DateTimeOffset CreatedAt)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<CrmOpportunity, OpportunitySummaryDto>> Projection = o => new OpportunitySummaryDto(
        o.Id,
        o.LegalEntityId,
        o.OpportunityNumber,
        o.Title,
        o.CustomerName,
        o.Stage,
        o.ProbabilityPercent,
        o.ExpectedRevenue,
        o.ExpectedCloseDate,
        o.AssignedUserName,
        o.CreatedAt);
}
