using ERP.Application.Common;

namespace ERP.Application.Auth;

/// <summary>Creates (or replaces, until enabled) the authenticator secret and returns it with the otpauth:// URI for the authenticator app.</summary>
public sealed record BeginTwoFactorSetupCommand : ICommand<TwoFactorSetup>;
