using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Puts a client on a plan (its first one, or a change), with the cycle, a custom price or free of charge. Modules outside the plan are switched off.</summary>
public sealed record SetClientPlanCommand(Guid OrgId, SetClientPlanInput Input) : ICommand<ClientDetail>;
