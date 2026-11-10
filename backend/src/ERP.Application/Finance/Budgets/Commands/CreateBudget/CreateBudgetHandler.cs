using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class CreateBudgetHandler(IBudgetRepository budgets, ILegalEntityGuard guard, BudgetMapper mapper) : ICommandHandler<CreateBudgetCommand, BudgetDto>
{
    public async Task<BudgetDto> Handle(CreateBudgetCommand cmd, CancellationToken ct)
    {
        // 1. A legal entity of the caller; one budget of that name per year.
        var entity = await guard.RequireAsync(cmd.Input.LegalEntityId, ct);
        if (await budgets.AnyAsync(b => b.LegalEntityId == entity.Id && b.FiscalYear == cmd.Input.FiscalYear && b.Name == cmd.Input.Name.Trim(), ct))
            throw new ConflictException("A budget with this name already exists for that year.");

        // 2. Apply the input and its lines, and save.
        var budget = new Budget { LegalEntityId = entity.Id };
        BudgetMapper.Apply(cmd.Input, budget);
        budget.Lines.AddRange(await mapper.LinesAsync(budget, cmd.Input, ct));
        budgets.Add(budget);
        await budgets.SaveChangesAsync(ct);
        return await mapper.ToDtoAsync(budget, ct);
    }
}
