using ERP.Application.Security;
using ERP.Application.Common;

namespace ERP.Modules.Hr.Features;

/// <summary>Rejects a submitted request; same rights as approving (checked in the handler).</summary>
public sealed record RejectLeaveRequestCommand(Guid Id, string? Comment) : ICommand<LeaveRequestDto>, IAuthorizesInHandler;
