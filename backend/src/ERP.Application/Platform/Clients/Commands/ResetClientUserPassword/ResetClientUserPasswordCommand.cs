using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Gives a client's user a temporary password and signs them out everywhere.</summary>
public sealed record ResetClientUserPasswordCommand(Guid OrgId, Guid UserId) : ICommand<TemporaryPassword>;
