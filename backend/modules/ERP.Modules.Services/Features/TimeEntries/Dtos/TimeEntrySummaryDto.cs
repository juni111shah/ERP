using System.Linq.Expressions;
using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Features;

public record TimeEntrySummaryDto(
    Guid Id,
    Guid LegalEntityId,
    Guid EngagementId,
    string ConsultantName,
    DateOnly ActivityDate,
    decimal Hours,
    decimal HourlyRate,
    decimal TotalAmount,
    string TaskDescription,
    bool IsBillable,
    bool IsApproved,
    DateTimeOffset CreatedAt,
    Guid? InvoiceId = null)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<TimeEntryRecord, TimeEntrySummaryDto>> Projection = t => new TimeEntrySummaryDto(
        t.Id,
        t.LegalEntityId,
        t.EngagementId,
        t.ConsultantName,
        t.ActivityDate,
        t.Hours,
        t.HourlyRate,
        t.TotalAmount,
        t.TaskDescription,
        t.IsBillable,
        t.IsApproved,
        t.CreatedAt,
        t.InvoiceId);
}
