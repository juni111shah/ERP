using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>The roster entries overlapping a month, by start date.</summary>
public sealed record ListRosterQuery(int Year, int Month) : IQuery<IReadOnlyList<RosterDto>>, IRequirePermission { public string RequiredPermission => P.AttendanceRead; }
