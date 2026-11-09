using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Features;

public record TimeEntryDto(
    Guid Id,
    Guid LegalEntityId,
    Guid EngagementId,
    Guid ConsultantUserId,
    string ConsultantName,
    DateOnly ActivityDate,
    decimal Hours,
    decimal HourlyRate,
    decimal TotalAmount,
    string TaskDescription,
    bool IsBillable,
    bool IsApproved,
    Guid? ApprovedByUserId,
    DateTimeOffset? ApprovedAt,
    string? CustomFields,
    DateTimeOffset CreatedAt,
    Guid? InvoiceId = null)
{
    public static TimeEntryDto From(TimeEntryRecord t) =>
        new(
            t.Id,
            t.LegalEntityId,
            t.EngagementId,
            t.ConsultantUserId,
            t.ConsultantName,
            t.ActivityDate,
            t.Hours,
            t.HourlyRate,
            t.TotalAmount,
            t.TaskDescription,
            t.IsBillable,
            t.IsApproved,
            t.ApprovedByUserId,
            t.ApprovedAt,
            t.CustomFields,
            t.CreatedAt,
            t.InvoiceId);
}
