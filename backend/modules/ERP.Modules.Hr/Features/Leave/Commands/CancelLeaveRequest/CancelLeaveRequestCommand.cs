using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Cancels a request; an approved one gives its days back to the balance.</summary>
public sealed record CancelLeaveRequestCommand(Guid Id) : ICommand<LeaveRequestDto>, IRequirePermission { public string RequiredPermission => P.LeaveRequest; }
