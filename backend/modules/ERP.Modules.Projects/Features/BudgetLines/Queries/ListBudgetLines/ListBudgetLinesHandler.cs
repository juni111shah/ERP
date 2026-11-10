using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class ListBudgetLinesHandler(
    IProjectBudgetLineRepository budgetLines,
    IProjectTimesheetRepository timesheets,
    IProjectExpenseRepository expenses,
    ProjectAccess access)
    : IQueryHandler<ListBudgetLinesQuery, IReadOnlyList<BudgetLineDto>>
{
    public async Task<IReadOnlyList<BudgetLineDto>> Handle(ListBudgetLinesQuery query, CancellationToken ct)
    {
        await access.EnsureProjectAsync(query.ProjectId, ct);
        var id = query.ProjectId;
        var lines = await budgetLines.ListOfProjectAsync(id, ct);
        var time = await timesheets.ListNotRejectedOfProjectAsync(id, ct);
        var costs = await expenses.ListApprovedOfProjectAsync(id, ct);
        return BudgetLines.Build(lines, time, costs);
    }
}
