using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Turns the month's daily logs into the monthly attendance record payroll reads (days present, unpaid absence, paid leave, overtime hours). Employees without logs are left alone.</summary>
public sealed record RollUpAttendanceCommand(int Year, int Month, Guid? LegalEntityId) : ICommand<IReadOnlyList<AttendanceRollUpRow>>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
