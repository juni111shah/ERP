using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Every active employee for a month with their recorded exceptions (or full attendance when nothing is recorded), plus the days of approved leave (read-only; payroll adds the unpaid ones to the recorded absences).</summary>
public sealed record ListAttendanceQuery(int Year, int Month, Guid? LegalEntityId) : IQuery<IReadOnlyList<AttendanceRowDto>>, IRequirePermission { public string RequiredPermission => P.AttendanceRead; }
