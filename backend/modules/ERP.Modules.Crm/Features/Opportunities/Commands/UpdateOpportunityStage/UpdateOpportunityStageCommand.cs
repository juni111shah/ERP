using ERP.Application.Common;
using ERP.Modules.Crm.Domain;

namespace ERP.Modules.Crm.Features;

/// <summary>Moves an opportunity to another pipeline stage (closing or reopening it when the stage says so).</summary>
public record UpdateOpportunityStageCommand(
    Guid Id,
    OpportunityStage Stage,
    int ProbabilityPercent) : ICommand<OpportunityDto>;
