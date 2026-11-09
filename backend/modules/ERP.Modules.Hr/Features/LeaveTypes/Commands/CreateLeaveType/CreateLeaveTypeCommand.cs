using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Creates a leave type (unique code) with its entitlement tiers and pay bands.</summary>
public sealed record CreateLeaveTypeCommand(LeaveTypeInput Input) : ICommand<LeaveTypeDto>, IRequirePermission { public string RequiredPermission => P.LeaveApprove; }
