using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class GetBalanceSheetHandler(
    TrialBalanceBuilder trialBalance, IJournalEntryRepository journals, IAccountRepository accounts, IFiscalYearCloseRepository closes, ISettingsService settings)
    : IQueryHandler<GetBalanceSheetQuery, BalanceSheetDto>
{
    public async Task<BalanceSheetDto> Handle(GetBalanceSheetQuery q, CancellationToken ct)
    {
        // 1. Closing balances on the date, by side.
        var rows = await trialBalance.BuildAsync(null, q.AsOf, q.LegalEntityId, ct);
        var assets = rows.Where(r => r.Type == AccountType.Asset).Select(r => new StatementLine(r.ReportingGroup ?? "Assets", r.Code, r.Name, r.Closing)).Where(l => l.Amount != 0).ToList();
        var liabilities = rows.Where(r => r.Type == AccountType.Liability).Select(r => new StatementLine(r.ReportingGroup ?? "Liabilities", r.Code, r.Name, -r.Closing)).Where(l => l.Amount != 0).ToList();
        var equity = rows.Where(r => r.Type == AccountType.Equity).Select(r => new StatementLine(r.ReportingGroup ?? "Equity", r.Code, r.Name, -r.Closing)).Where(l => l.Amount != 0).ToList();

        // 2. The income and expense not closed yet: this year's earnings, and what earlier unclosed years left behind.
        var unclosed = -rows.Where(r => r.Type is AccountType.Income or AccountType.Expense).Sum(r => r.Closing);
        var priorOpen = await PriorOpenEarningsAsync(q.AsOf, q.LegalEntityId, ct);
        var earnings = unclosed - priorOpen;

        // 3. Totals; balanced when assets equal liabilities, equity and earnings.
        var ta = assets.Sum(l => l.Amount); var tl = liabilities.Sum(l => l.Amount); var te = equity.Sum(l => l.Amount);
        return new BalanceSheetDto(q.AsOf, assets, liabilities, equity, ta, tl, te, earnings, ta == tl + te + earnings + priorOpen, priorOpen);
    }

    /// <summary>
    /// The result of fiscal years that ended before the one containing <paramref name="asOf"/> and are not closed yet, for entities that have closed a year by then.
    /// For them current earnings is the open year only and retained earnings holds what was closed. A legal entity that never closed keeps
    /// all of its unclosed result in current earnings, as it always has.
    /// </summary>
    private async Task<decimal> PriorOpenEarningsAsync(DateOnly asOf, Guid? entityId, CancellationToken ct)
    {
        var closedEntities = await closes.ListEntitiesClosedByAsync(asOf, entityId, ct);
        if (closedEntities.Count == 0) return 0m;
        var profitAndLoss = await accounts.ListIncomeAndExpenseIdsAsync(ct);
        var prior = 0m;
        foreach (var id in closedEntities)
        {
            var yearStart = FiscalYears.StartOf(asOf, await settings.GetAsync<int?>(FiscalYears.StartMonthSetting, id, ct) ?? 1);
            var lines = await journals.ListPostedAmountsBeforeAsync(id, yearStart, profitAndLoss, ct);
            prior += lines.Sum(l => l.Credit - l.Debit);
        }
        return prior;
    }
}
