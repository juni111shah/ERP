using System.Linq.Expressions;
using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Features;

public record MilestoneSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string MilestoneCode,
    Guid EngagementId,
    string Title,
    decimal Amount,
    DateOnly DueDate,
    DateOnly? CompletionDate,
    MilestoneStatus Status,
    DateTimeOffset CreatedAt)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<BillingMilestone, MilestoneSummaryDto>> Projection = m => new MilestoneSummaryDto(
        m.Id,
        m.LegalEntityId,
        m.MilestoneCode,
        m.EngagementId,
        m.Title,
        m.Amount,
        m.DueDate,
        m.CompletionDate,
        m.Status,
        m.CreatedAt);
}
