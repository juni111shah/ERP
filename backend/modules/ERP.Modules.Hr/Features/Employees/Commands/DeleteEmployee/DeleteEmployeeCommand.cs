using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Deletes an employee without payroll, leave, attendance or review history (others are terminated instead).</summary>
public sealed record DeleteEmployeeCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
