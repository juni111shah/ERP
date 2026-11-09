using ERP.Application.Common;

namespace ERP.Modules.Services.Features;

/// <summary>What can be corrected on an entry before approval. The engagement stays: to move time, delete the entry and log it on the right engagement. A null consultant keeps the current one.</summary>
public record UpdateTimeEntryCommand(
    Guid Id,
    DateOnly ActivityDate,
    decimal Hours,
    decimal HourlyRate,
    string TaskDescription,
    bool IsBillable,
    Guid? ConsultantUserId = null,
    string? ConsultantName = null,
    string? CustomFields = null) : ICommand<TimeEntryDto>;
