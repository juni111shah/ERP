using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Creates an onboarding or offboarding checklist template (unique name) with its steps.</summary>
public sealed record CreateChecklistTemplateCommand(ChecklistTemplateInput Input) : ICommand<ChecklistTemplateDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
