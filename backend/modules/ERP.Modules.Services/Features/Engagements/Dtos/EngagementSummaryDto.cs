using System.Linq.Expressions;
using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Features;

public record EngagementSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string EngagementCode,
    string Title,
    Guid ClientId,
    string ClientName,
    EngagementType Type,
    EngagementStatus Status,
    decimal ContractValue,
    decimal BudgetedHours,
    decimal ConsumedHours,
    DateOnly StartDate,
    DateOnly? TargetEndDate,
    string? LeadConsultantName,
    DateTimeOffset CreatedAt)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<ServiceEngagement, EngagementSummaryDto>> Projection = e => new EngagementSummaryDto(
        e.Id,
        e.LegalEntityId,
        e.EngagementCode,
        e.Title,
        e.ClientId,
        e.ClientName,
        e.Type,
        e.Status,
        e.ContractValue,
        e.BudgetedHours,
        e.ConsumedHours,
        e.StartDate,
        e.TargetEndDate,
        e.LeadConsultantName,
        e.CreatedAt);
}
