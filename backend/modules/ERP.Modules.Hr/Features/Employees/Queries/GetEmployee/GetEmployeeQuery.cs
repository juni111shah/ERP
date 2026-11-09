using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>One employee with the people details; pay only for callers with <c>hr.salary.read</c>.</summary>
public sealed record GetEmployeeQuery(Guid Id) : IQuery<EmployeeDto>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
