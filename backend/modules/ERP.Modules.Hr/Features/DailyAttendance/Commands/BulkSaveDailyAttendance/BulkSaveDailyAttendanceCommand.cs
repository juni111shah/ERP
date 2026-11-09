using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Imports many days of attendance; a row that cannot be saved is reported and skipped.</summary>
public sealed record BulkSaveDailyAttendanceCommand(IReadOnlyList<DailyAttendanceInput> Items) : ICommand<BulkDailyAttendanceResult>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
