using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Updates an employee and records what changed in the history; pay is only written by callers with <c>hr.salary.read</c>.</summary>
public sealed record UpdateEmployeeCommand(Guid Id, EmployeeInput Input) : ICommand<EmployeeDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
