using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Puts the employees on the shift (they leave any other one). An empty list clears nobody; use <see cref="UnassignShiftCommand"/>.</summary>
public sealed record AssignShiftCommand(Guid Id, IReadOnlyList<Guid> EmployeeIds) : ICommand<int>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
