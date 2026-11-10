using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Accepts a waiting registration and provisions its workspace.</summary>
public sealed record ApproveSignupCommand(Guid Id) : ICommand<SignupRow>;
