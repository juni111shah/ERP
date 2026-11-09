using ERP.Application.Common;
using ERP.Modules.Services.Domain;

namespace ERP.Modules.Services.Features;

/// <summary>Sets up a recurring retainer on an engagement, for the engagement's client.</summary>
public record CreateRetainerCommand(
    Guid LegalEntityId,
    Guid EngagementId,
    Guid ClientId,
    string ClientName,
    string Title,
    RetainerCycle Cycle,
    decimal RecurringFee,
    decimal IncludedHours,
    decimal OverageHourlyRate,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Terms,
    string? CustomFields) : ICommand<RetainerDto>;
