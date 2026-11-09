using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Deletes a monthly attendance record of a month not yet posted (the employee is then counted as fully present).</summary>
public sealed record DeleteAttendanceCommand(Guid RecordId) : ICommand, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
