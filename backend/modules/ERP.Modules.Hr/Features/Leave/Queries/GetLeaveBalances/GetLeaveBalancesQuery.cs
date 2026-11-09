using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Leave balances of a year per employee and active leave type: entitled, carried, taken, pending and remaining.</summary>
public sealed record GetLeaveBalancesQuery(int Year, Guid? EmployeeId, Guid? LegalEntityId) : IQuery<IReadOnlyList<LeaveBalanceRow>>, IRequirePermission { public string RequiredPermission => P.LeaveRead; }
