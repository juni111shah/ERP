using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>One employee document with how many days it has left.</summary>
public sealed record GetEmployeeDocumentQuery(Guid Id) : IQuery<EmployeeDocumentDto>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
