using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class GetExpenseHandler(IProjectExpenseRepository expenses, ProjectAccess access, ExpenseDtoBuilder dtos) : IQueryHandler<GetExpenseQuery, ExpenseDto>
{
    public async Task<ExpenseDto> Handle(GetExpenseQuery query, CancellationToken ct)
    {
        var e = await expenses.GetAsync(query.Id, ct);
        await access.EnsureProjectAsync(e.ProjectId, ct);
        return await dtos.BuildAsync(e, ct);
    }
}
