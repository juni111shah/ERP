using ERP.Application.Common;

namespace ERP.Application.Auth;

/// <summary>Issues the session for the workspace chosen after a sign-in that offered several.</summary>
public sealed record SelectWorkspaceCommand(string SelectionToken, Guid WorkspaceId, string? Ip) : ICommand<SessionResult>;
