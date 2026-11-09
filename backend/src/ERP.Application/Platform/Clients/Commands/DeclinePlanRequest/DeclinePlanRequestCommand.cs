using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Turns down the plan change a client asked for.</summary>
public sealed record DeclinePlanRequestCommand(Guid OrgId) : ICommand<ClientDetail>;
