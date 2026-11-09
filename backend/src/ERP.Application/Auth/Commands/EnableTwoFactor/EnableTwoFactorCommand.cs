using ERP.Application.Common;

namespace ERP.Application.Auth;

/// <summary>Confirms the first code from the app, turns two-factor on and returns the one-time recovery codes (shown once).</summary>
public sealed record EnableTwoFactorCommand(string Code) : ICommand<TwoFactorEnabled>;
