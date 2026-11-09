using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Turns down a waiting registration and tells the applicant.</summary>
public sealed record RejectSignupCommand(Guid Id, string? Reason) : ICommand<SignupRow>;
