using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Rosters the employees on the shift for a date range (it beats their default shift on those days).</summary>
public sealed record AddRosterCommand(Guid ShiftId, RosterInput Input) : ICommand<IReadOnlyList<RosterDto>>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
