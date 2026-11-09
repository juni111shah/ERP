using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Deletes an employee document.</summary>
public sealed record DeleteEmployeeDocumentCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
