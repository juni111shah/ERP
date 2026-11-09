using ERP.Application.Common;

namespace ERP.Application.Auth;

/// <summary>First sign-in without an authenticator: creates the key for the challenge from login and returns the QR / manual key.</summary>
public sealed record BeginTwoFactorChallengeSetupCommand(string ChallengeToken) : ICommand<TwoFactorChallengeSetup>;
