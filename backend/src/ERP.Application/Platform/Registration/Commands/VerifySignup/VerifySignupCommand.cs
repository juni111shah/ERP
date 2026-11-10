using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Confirms a registration's email with the link's token; then provisions it (open registration) or waits for review.</summary>
public sealed record VerifySignupCommand(string Token) : ICommand<SignupResult>;
