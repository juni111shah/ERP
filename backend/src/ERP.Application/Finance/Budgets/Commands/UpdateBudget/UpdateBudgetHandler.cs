using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class UpdateBudgetHandler(IBudgetRepository budgets, IRepository<BudgetLine> lineRows, BudgetMapper mapper) : ICommandHandler<UpdateBudgetCommand, BudgetDto>
{
    public async Task<BudgetDto> Handle(UpdateBudgetCommand cmd, CancellationToken ct)
    {
        // 1. Load it; its legal entity cannot change.
        var budget = await budgets.GetWithLinesForUpdateAsync(cmd.Id, ct);
        if (cmd.Input.LegalEntityId != budget.LegalEntityId) throw new FieldValidationException("legalEntityId", "The legal entity of a budget cannot change.");

        // 2. Apply the input and replace the lines, and save.
        BudgetMapper.Apply(cmd.Input, budget);
        foreach (var old in budget.Lines.ToList()) lineRows.Remove(old);
        var fresh = await mapper.LinesAsync(budget, cmd.Input, ct);
        lineRows.AddRange(fresh);
        await budgets.SaveChangesAsync(ct);

        // 3. Answer with the new lines.
        budget.Lines.Clear();
        budget.Lines.AddRange(fresh);
        return await mapper.ToDtoAsync(budget, ct);
    }
}
