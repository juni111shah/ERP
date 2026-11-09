using ERP.Application.Common;

namespace ERP.Application.Auth;

/// <summary>Moves the signed-in user to another of their workspaces; the current workspace's tokens are revoked.</summary>
public sealed record SwitchWorkspaceCommand(Guid WorkspaceId, bool Persistent, string? CurrentJti, DateTime? CurrentExp, string? Ip) : ICommand<SessionResult>;
