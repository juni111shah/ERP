using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Takes the employees off their shift (they fall back to the workspace settings).</summary>
public sealed record UnassignShiftCommand(IReadOnlyList<Guid> EmployeeIds) : ICommand<int>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
