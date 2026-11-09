using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>The project's budget by category against what was really spent.</summary>
public sealed record ListBudgetLinesQuery(Guid ProjectId) : IQuery<IReadOnlyList<BudgetLineDto>>, IRequirePermission
{
    public string RequiredPermission => P.ProjectsRead;
}
