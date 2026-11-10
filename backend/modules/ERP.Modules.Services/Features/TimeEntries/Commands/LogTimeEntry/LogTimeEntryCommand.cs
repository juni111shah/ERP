using ERP.Application.Common;

namespace ERP.Modules.Services.Features;

/// <summary>Logs time on an engagement and adds it to the engagement's consumed hours and cost.</summary>
public record LogTimeEntryCommand(
    Guid LegalEntityId,
    Guid EngagementId,
    Guid ConsultantUserId,
    string ConsultantName,
    DateOnly ActivityDate,
    decimal Hours,
    decimal HourlyRate,
    string TaskDescription,
    bool IsBillable,
    string? CustomFields) : ICommand<TimeEntryDto>;
