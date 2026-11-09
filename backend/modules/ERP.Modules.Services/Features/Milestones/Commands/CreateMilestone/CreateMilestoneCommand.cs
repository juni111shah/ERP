using ERP.Application.Common;

namespace ERP.Modules.Services.Features;

/// <summary>Plans a billing milestone on an engagement, within its contract value.</summary>
public record CreateMilestoneCommand(
    Guid LegalEntityId,
    Guid EngagementId,
    string Title,
    decimal Amount,
    DateOnly DueDate,
    string? Notes,
    string? CustomFields) : ICommand<MilestoneDto>;
