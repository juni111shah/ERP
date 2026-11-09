using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Features;

public record MilestoneDto(
    Guid Id,
    Guid LegalEntityId,
    string MilestoneCode,
    Guid EngagementId,
    string Title,
    decimal Amount,
    DateOnly DueDate,
    DateOnly? CompletionDate,
    MilestoneStatus Status,
    Guid? InvoiceId,
    string? Notes,
    string? CustomFields,
    DateTimeOffset CreatedAt)
{
    public static MilestoneDto From(BillingMilestone m) =>
        new(
            m.Id,
            m.LegalEntityId,
            m.MilestoneCode,
            m.EngagementId,
            m.Title,
            m.Amount,
            m.DueDate,
            m.CompletionDate,
            m.Status,
            m.InvoiceId,
            m.Notes,
            m.CustomFields,
            m.CreatedAt);
}
