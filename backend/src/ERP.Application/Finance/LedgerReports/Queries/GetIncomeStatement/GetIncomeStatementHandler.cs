using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class GetIncomeStatementHandler(TrialBalanceBuilder trialBalance) : IQueryHandler<GetIncomeStatementQuery, IncomeStatementDto>
{
    public async Task<IncomeStatementDto> Handle(GetIncomeStatementQuery q, CancellationToken ct)
    {
        var rows = await trialBalance.BuildAsync(q.From, q.To, q.LegalEntityId, ct, excludeYearEnd: true); // a closing entry moves a result to equity; it is not income or expense
        // Income is credit-natured, so its closing (Dr - Cr) is negative; present it positive.
        var income = rows.Where(r => r.Type == AccountType.Income).Select(r => new StatementLine(r.ReportingGroup ?? "Income", r.Code, r.Name, -(r.Debit - r.Credit))).Where(l => l.Amount != 0).ToList();
        var expenses = rows.Where(r => r.Type == AccountType.Expense).Select(r => new StatementLine(r.ReportingGroup ?? "Expenses", r.Code, r.Name, r.Debit - r.Credit)).Where(l => l.Amount != 0).ToList();
        var ti = income.Sum(l => l.Amount); var te = expenses.Sum(l => l.Amount);
        return new IncomeStatementDto(q.From, q.To, income, expenses, ti, te, ti - te);
    }
}
