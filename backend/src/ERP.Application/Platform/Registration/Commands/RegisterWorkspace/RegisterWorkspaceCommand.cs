using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>A registration from the public page: kept for review or email confirmation, or provisioned at once when registration is open.</summary>
public sealed record RegisterWorkspaceCommand(SignupInput Input, string? Ip) : ICommand<SignupResult>;
