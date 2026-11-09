using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetBudgetHandler(IBudgetRepository budgets, BudgetMapper mapper) : IQueryHandler<GetBudgetQuery, BudgetDto>
{
    public async Task<BudgetDto> Handle(GetBudgetQuery q, CancellationToken ct) => await mapper.ToDtoAsync(await budgets.GetWithLinesAsync(q.Id, ct), ct);
}
