using ERP.Application.Security;
using ERP.Application.Common;

namespace ERP.Modules.Hr.Features;

/// <summary>Needs <c>hr.leave.approve</c>, or being the line manager of the request's employee; checked in the handler (a manager need not hold the permission).</summary>
public sealed record ApproveLeaveRequestCommand(Guid Id, string? Comment) : ICommand<LeaveRequestDto>, IAuthorizesInHandler;
