using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Records a document of an employee (passport, visa, labour card ...).</summary>
public sealed record CreateEmployeeDocumentCommand(EmployeeDocumentInput Input) : ICommand<EmployeeDocumentDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
