using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>An employee's history, newest first; pay values hidden without <c>hr.salary.read</c>.</summary>
public sealed record GetEmployeeHistoryQuery(Guid EmployeeId) : IQuery<IReadOnlyList<EmployeeHistoryDto>>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
