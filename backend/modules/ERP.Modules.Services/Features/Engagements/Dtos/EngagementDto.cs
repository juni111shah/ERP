using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Features;

public record EngagementDto(
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
    decimal ConsumedCost,
    DateOnly StartDate,
    DateOnly? TargetEndDate,
    DateOnly? ActualEndDate,
    Guid? LeadConsultantUserId,
    string? LeadConsultantName,
    string? Description,
    string? CustomFields,
    DateTimeOffset CreatedAt)
{
    public static EngagementDto From(ServiceEngagement e) =>
        new(
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
            e.ConsumedCost,
            e.StartDate,
            e.TargetEndDate,
            e.ActualEndDate,
            e.LeadConsultantUserId,
            e.LeadConsultantName,
            e.Description,
            e.CustomFields,
            e.CreatedAt);
}
