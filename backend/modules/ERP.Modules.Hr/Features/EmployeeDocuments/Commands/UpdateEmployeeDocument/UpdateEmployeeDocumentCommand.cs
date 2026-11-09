using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Updates an employee document; it stays with its employee.</summary>
public sealed record UpdateEmployeeDocumentCommand(Guid Id, EmployeeDocumentInput Input) : ICommand<EmployeeDocumentDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
