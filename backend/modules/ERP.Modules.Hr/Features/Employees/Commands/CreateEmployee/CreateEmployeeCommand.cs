using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Creates an employee (numbered from the sequence when no number is given) and records the joining in the history.</summary>
public sealed record CreateEmployeeCommand(EmployeeInput Input) : ICommand<EmployeeDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
