using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Finance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public sealed record TrialBalanceRow(Guid AccountId, string Code, string Name, AccountType Type, string? ReportingGroup, decimal Opening, decimal Debit, decimal Credit, decimal Closing);
public sealed record TrialBalanceDto(DateOnly? From, DateOnly To, IReadOnlyList<TrialBalanceRow> Rows, decimal TotalDebit, decimal TotalCredit, bool Balanced);

public sealed record LedgerLineDto(Guid JournalId, string Number, DateOnly Date, string? Memo, string? Description, decimal Debit, decimal Credit, decimal Balance);
public sealed record GeneralLedgerDto(Guid AccountId, string Code, string Name, DateOnly? From, DateOnly To, decimal Opening, IReadOnlyList<LedgerLineDto> Lines, decimal Closing);

public sealed record StatementLine(string Group, string Code, string Name, decimal Amount);
public sealed record IncomeStatementDto(DateOnly From, DateOnly To, IReadOnlyList<StatementLine> Income, IReadOnlyList<StatementLine> Expenses, decimal TotalIncome, decimal TotalExpenses, decimal NetProfit);
public sealed record BalanceSheetDto(
    DateOnly AsOf, IReadOnlyList<StatementLine> Assets, IReadOnlyList<StatementLine> Liabilities, IReadOnlyList<StatementLine> Equity,
    decimal TotalAssets, decimal TotalLiabilities, decimal TotalEquity, decimal CurrentEarnings, bool Balanced);

/// <summary>Trial balance for a period (opening balance, movement, closing balance per account). Draft journals never count.</summary>
public sealed record GetTrialBalance(DateOnly? From, DateOnly To, Guid? LegalEntityId) : IQuery<TrialBalanceDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
public sealed record GetGeneralLedger(Guid AccountId, DateOnly? From, DateOnly To, Guid? LegalEntityId) : IQuery<GeneralLedgerDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
public sealed record GetIncomeStatement(DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<IncomeStatementDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
public sealed record GetBalanceSheet(DateOnly AsOf, Guid? LegalEntityId) : IQuery<BalanceSheetDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}

internal sealed class LedgerReportHandlers(IReadRepository<JournalEntry> journals, IReadRepository<Account> accounts) :
    IRequestHandler<GetTrialBalance, TrialBalanceDto>,
    IRequestHandler<GetGeneralLedger, GeneralLedgerDto>,
    IRequestHandler<GetIncomeStatement, IncomeStatementDto>,
    IRequestHandler<GetBalanceSheet, BalanceSheetDto>
{
    private sealed record Movement(Guid AccountId, DateOnly Date, decimal Debit, decimal Credit);

    public async Task<TrialBalanceDto> Handle(GetTrialBalance q, CancellationToken ct)
    {
        var rows = await BuildAsync(q.From, q.To, q.LegalEntityId, ct);
        return new TrialBalanceDto(q.From, q.To, rows, rows.Sum(r => r.Debit), rows.Sum(r => r.Credit), rows.Sum(r => r.Closing) == 0);
    }

    public async Task<GeneralLedgerDto> Handle(GetGeneralLedger q, CancellationToken ct)
    {
        var account = await accounts.GetAsync(q.AccountId, ct);
        var entityId = q.LegalEntityId;
        var lines = await journals.Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.Date <= q.To && (entityId == null || j.LegalEntityId == entityId)))
            .SelectMany(j => j.Lines.Where(l => l.AccountId == q.AccountId), (j, l) => new { j.Id, j.Number, j.Date, j.Memo, l.Description, l.Debit, l.Credit, l.Seq })
            .ToListAsync(ct);
        var opening = q.From is { } from ? lines.Where(l => l.Date < from).Sum(l => l.Debit - l.Credit) : 0m;
        var running = opening;
        var inRange = new List<LedgerLineDto>();
        foreach (var l in lines.Where(l => q.From is not { } f || l.Date >= f).OrderBy(l => l.Date).ThenBy(l => l.Number).ThenBy(l => l.Seq))
        {
            running += l.Debit - l.Credit;
            inRange.Add(new LedgerLineDto(l.Id, l.Number, l.Date, l.Memo, l.Description, l.Debit, l.Credit, running));
        }
        return new GeneralLedgerDto(account.Id, account.Code, account.Name, q.From, q.To, opening, inRange, running);
    }

    public async Task<IncomeStatementDto> Handle(GetIncomeStatement q, CancellationToken ct)
    {
        var rows = await BuildAsync(q.From, q.To, q.LegalEntityId, ct);
        // Income is credit-natured, so its closing (Dr - Cr) is negative; present it positive.
        var income = rows.Where(r => r.Type == AccountType.Income).Select(r => new StatementLine(r.ReportingGroup ?? "Income", r.Code, r.Name, -(r.Debit - r.Credit))).Where(l => l.Amount != 0).ToList();
        var expenses = rows.Where(r => r.Type == AccountType.Expense).Select(r => new StatementLine(r.ReportingGroup ?? "Expenses", r.Code, r.Name, r.Debit - r.Credit)).Where(l => l.Amount != 0).ToList();
        var ti = income.Sum(l => l.Amount); var te = expenses.Sum(l => l.Amount);
        return new IncomeStatementDto(q.From, q.To, income, expenses, ti, te, ti - te);
    }

    public async Task<BalanceSheetDto> Handle(GetBalanceSheet q, CancellationToken ct)
    {
        var rows = await BuildAsync(null, q.AsOf, q.LegalEntityId, ct);
        var assets = rows.Where(r => r.Type == AccountType.Asset).Select(r => new StatementLine(r.ReportingGroup ?? "Assets", r.Code, r.Name, r.Closing)).Where(l => l.Amount != 0).ToList();
        var liabilities = rows.Where(r => r.Type == AccountType.Liability).Select(r => new StatementLine(r.ReportingGroup ?? "Liabilities", r.Code, r.Name, -r.Closing)).Where(l => l.Amount != 0).ToList();
        var equity = rows.Where(r => r.Type == AccountType.Equity).Select(r => new StatementLine(r.ReportingGroup ?? "Equity", r.Code, r.Name, -r.Closing)).Where(l => l.Amount != 0).ToList();
        var earnings = -rows.Where(r => r.Type is AccountType.Income or AccountType.Expense).Sum(r => r.Closing);
        var ta = assets.Sum(l => l.Amount); var tl = liabilities.Sum(l => l.Amount); var te = equity.Sum(l => l.Amount);
        return new BalanceSheetDto(q.AsOf, assets, liabilities, equity, ta, tl, te, earnings, ta == tl + te + earnings);
    }

    private async Task<List<TrialBalanceRow>> BuildAsync(DateOnly? from, DateOnly to, Guid? entityId, CancellationToken ct)
    {
        // Sum on the client: decimal SUM is not portable across the supported databases and the projection is only three columns.
        var movements = await journals.Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.Date <= to && (entityId == null || j.LegalEntityId == entityId)))
            .SelectMany(j => j.Lines, (j, l) => new Movement(l.AccountId, j.Date, l.Debit, l.Credit))
            .ToListAsync(ct);
        var byAccount = movements.GroupBy(m => m.AccountId).ToDictionary(g => g.Key, g => g.ToList());
        var list = await accounts.Query().OrderBy(a => a.Code).Select(a => new { a.Id, a.Code, a.Name, a.Type, a.ReportingGroup }).ToListAsync(ct);
        var result = new List<TrialBalanceRow>();
        foreach (var a in list)
        {
            if (!byAccount.TryGetValue(a.Id, out var ms)) continue;
            var before = from is { } f ? ms.Where(m => m.Date < f) : Enumerable.Empty<Movement>();
            var within = from is { } f2 ? ms.Where(m => m.Date >= f2) : ms;
            var opening = before.Sum(m => m.Debit - m.Credit);
            var debit = within.Sum(m => m.Debit); var credit = within.Sum(m => m.Credit);
            if (opening == 0 && debit == 0 && credit == 0) continue;
            result.Add(new TrialBalanceRow(a.Id, a.Code, a.Name, a.Type, a.ReportingGroup, opening, debit, credit, opening + debit - credit));
        }
        return result;
    }
}
