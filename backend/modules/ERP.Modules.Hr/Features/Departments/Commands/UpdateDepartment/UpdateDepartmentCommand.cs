using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Updates a department; a changed code stays unique and the hierarchy may not loop.</summary>
public sealed record UpdateDepartmentCommand(Guid Id, DepartmentInput Input) : ICommand<DepartmentDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
