using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Hr.Features;

/// <summary>Deletes a checklist template with its steps (tasks already created stay).</summary>
public sealed record DeleteChecklistTemplateCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
