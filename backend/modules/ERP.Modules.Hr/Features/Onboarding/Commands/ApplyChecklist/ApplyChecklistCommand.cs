using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Creates the template's steps as tasks of the employee. A template can be applied once per employee.</summary>
public sealed record ApplyChecklistCommand(Guid EmployeeId, Guid TemplateId) : ICommand<IReadOnlyList<EmployeeTaskDto>>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
