using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class GetBudgetVsActualHandler(IBudgetRepository budgets, IJournalEntryRepository journals, IAccountRepository accounts)
    : IQueryHandler<GetBudgetVsActualQuery, BudgetVsActualDto>
{
    public async Task<BudgetVsActualDto> Handle(GetBudgetVsActualQuery q, CancellationToken ct)
    {
        // 1. The budget and the posted movement of its months (year-end closing entries left out).
        var budget = await budgets.GetWithLinesAsync(q.BudgetId, ct);
        var from = new DateOnly(budget.FiscalYear, q.FromMonth, 1);
        var to = new DateOnly(budget.FiscalYear, q.ToMonth, 1).AddMonths(1).AddDays(-1);
        var movements = await journals.ListPostedMovementsExcludingYearEndAsync(budget.LegalEntityId, from, to, ct);

        // 2. Budget and actual per income and expense account.
        var budgeted = budget.Lines.Where(l => l.Month >= q.FromMonth && l.Month <= q.ToMonth).GroupBy(l => l.AccountId).ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));
        var actualByAccount = movements.GroupBy(m => m.AccountId).ToDictionary(g => g.Key, g => g.Sum(m => m.Debit - m.Credit));
        var ids = budgeted.Keys.Union(actualByAccount.Keys).ToList();
        var accountRows = await accounts.ListIncomeAndExpenseHeadersByCodeAsync(ids, ct);
        var rows = accountRows.Select(a =>
        {
            var plan = budgeted.GetValueOrDefault(a.Id);
            var net = actualByAccount.GetValueOrDefault(a.Id); // debit − credit
            var actual = a.Type == AccountType.Income ? -net : net;
            var variance = a.Type == AccountType.Income ? actual - plan : plan - actual;
            return new BudgetVsActualRow(a.Id, a.Code, a.Name, a.Type, plan, actual, variance, plan == 0 ? null : Math.Round(variance / plan * 100, 1));
        }).Where(r => r.Budget != 0 || r.Actual != 0).ToList();

        // 3. Totals.
        decimal Sum(AccountType t, Func<BudgetVsActualRow, decimal> f) => rows.Where(r => r.Type == t).Sum(f);
        var bi = Sum(AccountType.Income, r => r.Budget); var ai = Sum(AccountType.Income, r => r.Actual);
        var be = Sum(AccountType.Expense, r => r.Budget); var ae = Sum(AccountType.Expense, r => r.Actual);
        return new(budget.Id, budget.Name, budget.FiscalYear, q.FromMonth, q.ToMonth, rows, bi, ai, be, ae, bi - be, ai - ae);
    }
}
