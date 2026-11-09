using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Deletes one day's attendance log.</summary>
public sealed record DeleteDailyAttendanceCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
