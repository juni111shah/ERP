using ERP.Application.Common;

namespace ERP.Application.Auth;

/// <summary>Changes the signed-in user's password after checking the current one.</summary>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand;
