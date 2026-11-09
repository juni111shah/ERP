using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>One leave request.</summary>
public sealed record GetLeaveRequestQuery(Guid Id) : IQuery<LeaveRequestDto>, IRequirePermission { public string RequiredPermission => P.LeaveRead; }
