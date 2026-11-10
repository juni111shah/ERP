using ERP.Application.Common;

namespace ERP.Application.Auth;

/// <summary>Confirms a login challenge (setup or returning user) with a code and issues the session.</summary>
public sealed record VerifyTwoFactorChallengeCommand(string ChallengeToken, string Code, string? Ip) : ICommand<TwoFactorChallengeResult>;
