using System.Linq.Expressions;
using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Features;

public record RetainerSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string RetainerNumber,
    Guid EngagementId,
    string ClientName,
    string Title,
    RetainerCycle Cycle,
    RetainerStatus Status,
    decimal RecurringFee,
    decimal IncludedHours,
    DateOnly StartDate,
    DateTimeOffset CreatedAt)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<RetainerContract, RetainerSummaryDto>> Projection = r => new RetainerSummaryDto(
        r.Id,
        r.LegalEntityId,
        r.RetainerNumber,
        r.EngagementId,
        r.ClientName,
        r.Title,
        r.Cycle,
        r.Status,
        r.RecurringFee,
        r.IncludedHours,
        r.StartDate,
        r.CreatedAt);
}
