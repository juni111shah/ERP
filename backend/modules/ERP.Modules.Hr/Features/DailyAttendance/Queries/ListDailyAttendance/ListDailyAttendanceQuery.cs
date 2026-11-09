using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>The daily attendance logs of a month, of one employee and/or one legal entity.</summary>
public sealed record ListDailyAttendanceQuery(int Year, int Month, Guid? EmployeeId, Guid? LegalEntityId) : IQuery<IReadOnlyList<DailyAttendanceDto>>, IRequirePermission { public string RequiredPermission => P.AttendanceRead; }
