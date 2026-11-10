using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Submits a draft request for approval (approved at once when the type needs no approval).</summary>
public sealed record SubmitLeaveRequestCommand(Guid Id) : ICommand<LeaveRequestDto>, IRequirePermission { public string RequiredPermission => P.LeaveRequest; }
