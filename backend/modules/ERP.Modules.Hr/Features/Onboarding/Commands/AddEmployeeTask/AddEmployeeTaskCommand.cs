using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Adds a one-off task to an employee.</summary>
public sealed record AddEmployeeTaskCommand(Guid EmployeeId, EmployeeTaskInput Input) : ICommand<EmployeeTaskDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
