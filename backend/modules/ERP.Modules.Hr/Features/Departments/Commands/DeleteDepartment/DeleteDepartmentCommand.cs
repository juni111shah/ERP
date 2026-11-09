using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Deletes a department that has no employees and no sub-departments.</summary>
public sealed record DeleteDepartmentCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
