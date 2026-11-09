using ERP.Application.Common;

namespace ERP.Modules.Crm.Features;

/// <summary>Closes an open opportunity as won or lost.</summary>
public record CloseOpportunityCommand(
    Guid Id,
    bool IsWon,
    decimal ActualRevenue,
    string? CloseReason) : ICommand<OpportunityDto>;
