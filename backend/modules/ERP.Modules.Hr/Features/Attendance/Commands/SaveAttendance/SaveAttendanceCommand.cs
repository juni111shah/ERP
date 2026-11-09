using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Records (or replaces) an employee's attendance exceptions for a month not yet posted.</summary>
public sealed record SaveAttendanceCommand(AttendanceInput Input) : ICommand<AttendanceRowDto>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
