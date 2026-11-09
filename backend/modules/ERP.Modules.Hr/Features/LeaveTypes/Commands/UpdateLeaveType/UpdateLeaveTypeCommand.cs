using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Updates a leave type; tiers and pay bands are replaced when given.</summary>
public sealed record UpdateLeaveTypeCommand(Guid Id, LeaveTypeInput Input) : ICommand<LeaveTypeDto>, IRequirePermission { public string RequiredPermission => P.LeaveApprove; }
