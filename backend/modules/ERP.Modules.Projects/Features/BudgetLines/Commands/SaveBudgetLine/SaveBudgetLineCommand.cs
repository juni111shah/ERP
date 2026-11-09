using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Creates (no id) or changes the budget of one category of a project.</summary>
public sealed record SaveBudgetLineCommand(Guid? Id, BudgetLineInput Input) : ICommand<BudgetLineDto>, IRequirePermission
{
    public string RequiredPermission => P.ProjectsManage;
}
