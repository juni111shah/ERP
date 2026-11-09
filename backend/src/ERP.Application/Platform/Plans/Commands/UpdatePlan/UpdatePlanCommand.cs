using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Changes a plan. Clients already on it keep their custom price, if any.</summary>
public sealed record UpdatePlanCommand(Guid Id, PlanInput Input) : ICommand<PlanView>;
