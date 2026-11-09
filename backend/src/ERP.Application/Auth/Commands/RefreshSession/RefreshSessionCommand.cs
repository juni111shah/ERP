using ERP.Application.Common;

namespace ERP.Application.Auth;

/// <summary>Exchanges the refresh cookie for a new session (the refresh token rotates).</summary>
public sealed record RefreshSessionCommand(string? RefreshToken, string? Ip) : ICommand<SessionResult>;
