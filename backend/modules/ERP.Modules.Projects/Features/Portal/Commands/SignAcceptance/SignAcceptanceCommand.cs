using ERP.Application.Common;

namespace ERP.Modules.Projects.Features;

/// <summary>The customer signs (types their name) or declines. Signing a milestone's sign-off marks the milestone as achieved. <see cref="ClientIp"/> is kept as evidence.</summary>
public sealed record SignAcceptanceCommand(Guid Id, SignAcceptanceInput Input, string? ClientIp = null) : ICommand<PortalAcceptanceDto>;
