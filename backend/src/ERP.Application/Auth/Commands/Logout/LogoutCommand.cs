using ERP.Application.Common;

namespace ERP.Application.Auth;

/// <summary>Revokes the refresh token and the current access token.</summary>
public sealed record LogoutCommand(string? RefreshToken, string? Jti, DateTime? Exp) : ICommand;
