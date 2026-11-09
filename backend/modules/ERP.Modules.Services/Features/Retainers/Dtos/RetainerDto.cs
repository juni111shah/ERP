using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Features;

public record RetainerDto(
    Guid Id,
    Guid LegalEntityId,
    string RetainerNumber,
    Guid EngagementId,
    Guid ClientId,
    string ClientName,
    string Title,
    RetainerCycle Cycle,
    RetainerStatus Status,
    decimal RecurringFee,
    decimal IncludedHours,
    decimal OverageHourlyRate,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Terms,
    string? CustomFields,
    DateTimeOffset CreatedAt)
{
    public static RetainerDto From(RetainerContract r) =>
        new(
            r.Id,
            r.LegalEntityId,
            r.RetainerNumber,
            r.EngagementId,
            r.ClientId,
            r.ClientName,
            r.Title,
            r.Cycle,
            r.Status,
            r.RecurringFee,
            r.IncludedHours,
            r.OverageHourlyRate,
            r.StartDate,
            r.EndDate,
            r.Terms,
            r.CustomFields,
            r.CreatedAt);
}
