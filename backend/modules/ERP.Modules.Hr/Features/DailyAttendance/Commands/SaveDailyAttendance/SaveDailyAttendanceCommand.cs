using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Records (or replaces) one employee's attendance of one day.</summary>
public sealed record SaveDailyAttendanceCommand(DailyAttendanceInput Input) : ICommand<DailyAttendanceDto>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
