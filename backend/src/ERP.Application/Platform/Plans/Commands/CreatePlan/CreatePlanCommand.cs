using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Adds a plan to the price list.</summary>
public sealed record CreatePlanCommand(PlanInput Input) : ICommand<PlanView>;
