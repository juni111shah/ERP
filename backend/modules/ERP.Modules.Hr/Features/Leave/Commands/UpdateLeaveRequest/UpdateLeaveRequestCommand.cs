using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Edits a draft request (same employee), submitting it when asked.</summary>
public sealed record UpdateLeaveRequestCommand(Guid Id, LeaveRequestInput Input) : ICommand<LeaveRequestDto>, IRequirePermission { public string RequiredPermission => P.LeaveRequest; }
