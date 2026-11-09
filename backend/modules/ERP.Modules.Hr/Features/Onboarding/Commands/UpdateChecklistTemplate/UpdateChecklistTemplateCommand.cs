using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Updates a checklist template; the steps are replaced when given.</summary>
public sealed record UpdateChecklistTemplateCommand(Guid Id, ChecklistTemplateInput Input) : ICommand<ChecklistTemplateDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
