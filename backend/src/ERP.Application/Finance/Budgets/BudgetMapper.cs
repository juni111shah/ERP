using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Applies a budget's input and builds its answer, shared by the budget commands and queries.</summary>
public sealed class BudgetMapper(IAccountRepository accounts)
{
    public static void Apply(BudgetInput i, Budget b) { b.Name = i.Name.Trim(); b.FiscalYear = i.FiscalYear; b.Notes = i.Notes; b.IsActive = i.IsActive; }

    /// <summary>The new lines of the budget (zero amounts dropped). Every line must use an income or expense account.</summary>
    public async Task<List<BudgetLine>> LinesAsync(Budget budget, BudgetInput input, CancellationToken ct)
    {
        var ids = input.Lines.Select(l => l.AccountId).Distinct().ToList();
        var found = await accounts.MapTypesAsync(ids, ct);
        foreach (var l in input.Lines)
            if (!found.TryGetValue(l.AccountId, out var type) || type is not (AccountType.Income or AccountType.Expense))
                throw new FieldValidationException("lines", "Budget lines must use income or expense accounts.");
        return input.Lines.Where(l => l.Amount != 0).Select(l => new BudgetLine { BudgetId = budget.Id, AccountId = l.AccountId, Month = l.Month, Amount = l.Amount }).ToList();
    }

    public async Task<BudgetDto> ToDtoAsync(Budget b, CancellationToken ct)
    {
        var ids = b.Lines.Select(l => l.AccountId).Distinct().ToList();
        var map = await accounts.MapHeadersAsync(ids, ct);
        var lines = b.Lines.OrderBy(l => map[l.AccountId].Code).ThenBy(l => l.Month).Select(l => new BudgetLineDto(l.AccountId, map[l.AccountId].Code, map[l.AccountId].Name, l.Month, l.Amount)).ToList();
        return new(b.Id, b.LegalEntityId, b.Name, b.FiscalYear, b.Notes, b.IsActive,
            b.Lines.Where(l => map[l.AccountId].Type == AccountType.Income).Sum(l => l.Amount), b.Lines.Where(l => map[l.AccountId].Type == AccountType.Expense).Sum(l => l.Amount), lines);
    }
}
