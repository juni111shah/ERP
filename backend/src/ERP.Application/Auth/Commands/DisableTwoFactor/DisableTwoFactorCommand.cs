using ERP.Application.Common;

namespace ERP.Application.Auth;

/// <summary>Turns two-factor off after checking the password and a current code.</summary>
public sealed record DisableTwoFactorCommand(string Password, string Code) : ICommand;
