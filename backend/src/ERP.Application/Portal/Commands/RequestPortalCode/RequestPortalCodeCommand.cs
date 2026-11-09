using ERP.Application.Common;

namespace ERP.Application.Portal;

/// <summary>Emails a one-time sign-in code. Kind: "customer" (tenant) or "vendor"; omitted = customer when the email belongs to a customer, otherwise vendor.</summary>
public sealed record RequestPortalCodeCommand(string Workspace, string Email, string? Kind = null) : ICommand;
