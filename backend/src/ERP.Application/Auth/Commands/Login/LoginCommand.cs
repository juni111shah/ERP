using ERP.Application.Common;

namespace ERP.Application.Auth;

// Cookies, headers and the client IP are HTTP concerns: the controller reads them and passes plain values in.

/// <summary>Checks email and password (and an inline two-factor code from older app builds) and answers with the session, the workspace choice or a two-factor challenge.</summary>
public sealed record LoginCommand(string Email, string Password, bool RememberMe, string? Ip, string? TwoFactorCode = null) : ICommand<LoginResult>;
