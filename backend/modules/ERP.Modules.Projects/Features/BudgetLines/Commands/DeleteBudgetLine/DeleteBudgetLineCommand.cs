using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Deletes a budget line.</summary>
public sealed record DeleteBudgetLineCommand(Guid Id) : ICommand, IRequirePermission
{
    public string RequiredPermission => P.ProjectsManage;
}
