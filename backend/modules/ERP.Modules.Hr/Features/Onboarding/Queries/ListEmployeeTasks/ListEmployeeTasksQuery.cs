using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>An employee's tasks, open ones first, by due date.</summary>
public sealed record ListEmployeeTasksQuery(Guid EmployeeId) : IQuery<IReadOnlyList<EmployeeTaskDto>>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
