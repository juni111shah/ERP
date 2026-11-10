using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Creates a draft request, or submits it straight away with Submit = true.</summary>
public sealed record CreateLeaveRequestCommand(LeaveRequestInput Input) : ICommand<LeaveRequestDto>, IRequirePermission { public string RequiredPermission => P.LeaveRequest; }
