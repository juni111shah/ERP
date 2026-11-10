using ERP.Application.Common;

namespace ERP.Application.Auth;

/// <summary>Sets a new password with the token from the reset email.</summary>
public sealed record ResetPasswordCommand(string Token, string NewPassword) : ICommand;
