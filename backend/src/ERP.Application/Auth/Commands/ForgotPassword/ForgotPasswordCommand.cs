using ERP.Application.Common;

namespace ERP.Application.Auth;

/// <summary>Sends a password reset link when the email has an account. The answer is the same either way.</summary>
public sealed record ForgotPasswordCommand(string Email) : ICommand;
