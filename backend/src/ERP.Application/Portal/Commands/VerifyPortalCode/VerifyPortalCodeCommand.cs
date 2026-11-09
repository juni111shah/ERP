using ERP.Application.Common;

namespace ERP.Application.Portal;

/// <summary>Exchanges a sign-in code for a portal session token.</summary>
public sealed record VerifyPortalCodeCommand(string Workspace, string Email, string Code, string? Kind = null) : ICommand<PortalSession>;
