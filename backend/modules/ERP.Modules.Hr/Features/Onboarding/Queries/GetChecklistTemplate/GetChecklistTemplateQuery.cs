using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>One checklist template with its steps.</summary>
public sealed record GetChecklistTemplateQuery(Guid Id) : IQuery<ChecklistTemplateDto>, IRequirePermission { public string RequiredPermission => P.EmployeesRead; }
