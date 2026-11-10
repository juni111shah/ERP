using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Creates a department with a unique (upper-case) code.</summary>
public sealed record CreateDepartmentCommand(DepartmentInput Input) : ICommand<DepartmentDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
