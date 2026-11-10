using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Deletes an employee task.</summary>
public sealed record DeleteEmployeeTaskCommand(Guid TaskId) : ICommand, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
