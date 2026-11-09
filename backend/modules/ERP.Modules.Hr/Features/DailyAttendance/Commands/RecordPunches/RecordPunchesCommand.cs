using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Folds raw clock punches (by employee number) into the days' check-in and check-out; a punch that cannot be used is reported and skipped.</summary>
public sealed record RecordPunchesCommand(IReadOnlyList<PunchInput> Punches, string? Source) : ICommand<BulkDailyAttendanceResult>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
