using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Finance;

public enum CashFlowCategory { Operating, Investing, Financing }

public sealed record CashFlowLine(string Group, string Code, string Name, decimal Inflows, decimal Outflows, decimal Net);
public sealed record CashFlowSection(CashFlowCategory Category, IReadOnlyList<CashFlowLine> Lines, decimal Inflows, decimal Outflows, decimal Net);

/// <summary>
/// Direct-method statement. <see cref="OpeningBalance"/> + <see cref="NetChange"/> must equal the bank and cash balance in the ledger at the end of the
/// period (<see cref="LedgerClosingBalance"/>); <see cref="TiesOut"/> says it does. <see cref="InternalTransfers"/> is money moved between the entity's own bank
/// and cash accounts: it is shown for information and is not a cash flow.
/// </summary>
public sealed record CashFlowStatementDto(
    DateOnly From, DateOnly To, Guid? LegalEntityId, string? CurrencyCode, decimal OpeningBalance, IReadOnlyList<CashFlowSection> Sections, decimal NetChange, decimal ClosingBalance,
    decimal LedgerClosingBalance, bool TiesOut, decimal InternalTransfers);

/// <summary>Cash flow statement for a date range, built from the journal lines of bank and cash accounts and classified by what is on the other side of each entry.</summary>
public sealed record GetCashFlowStatement(DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<CashFlowStatementDto>, IRequirePermission { public string RequiredPermission => CorePermissions.ReportsRead; }

public sealed class GetCashFlowStatementValidator : AbstractValidator<GetCashFlowStatement>
{
    public GetCashFlowStatementValidator() => ReportRange.Apply(this, x => (DateOnly?)x.From, x => x.To);
}

/// <summary>
/// Which section an account's cash belongs to, from its type and reporting group (pure). Equity is financing. System accounts (receivables, payables, tax, inventory,
/// payroll...) are the operating cycle whatever their group says. Otherwise non-current asset groups are investing, non-current liability and borrowing groups are
/// financing, and everything else is operating.
/// </summary>
public static class CashFlowClassifier
{
    public static CashFlowCategory Classify(AccountType type, string? reportingGroup, string? systemRole)
    {
        if (type == AccountType.Equity) return CashFlowCategory.Financing;
        if (systemRole is not null) return CashFlowCategory.Operating;
        var group = (reportingGroup ?? "").ToLowerInvariant();
        if (type == AccountType.Asset && (group.Contains("non-current") || group.Contains("noncurrent") || group.Contains("fixed asset") || group.Contains("investment"))) return CashFlowCategory.Investing;
        if (type == AccountType.Liability && (group.Contains("non-current") || group.Contains("noncurrent") || group.Contains("loan") || group.Contains("borrowing"))) return CashFlowCategory.Financing;
        return CashFlowCategory.Operating;
    }
}

/// <summary>Splits the net cash of one journal over the entries on the other side of it (pure).</summary>
public static class CashFlowAllocator
{
    public sealed record EntryLine(Guid AccountId, decimal Debit, decimal Credit);

    /// <summary>
    /// Positive amounts are inflows. Money in (net debit to cash) comes from the non-cash credits and money out goes to the non-cash debits, in proportion, to the cent;
    /// the last line takes the rounding so the parts always add up to the cash. Lines on the same side as the cash (the accumulated depreciation
    /// written off in a disposal, say) moved no cash. A journal that only moves money between cash accounts allocates nothing.
    /// </summary>
    public static IReadOnlyList<(Guid AccountId, decimal Amount)> Allocate(IReadOnlyCollection<EntryLine> lines, IReadOnlySet<Guid> cashAccounts)
    {
        var net = lines.Where(l => cashAccounts.Contains(l.AccountId)).Sum(l => l.Debit - l.Credit);
        if (net == 0) return [];
        var other = lines.Where(l => !cashAccounts.Contains(l.AccountId))
            .Select(l => (l.AccountId, Amount: net > 0 ? l.Credit : l.Debit)).Where(l => l.Amount > 0).ToList();
        var total = other.Sum(l => l.Amount);
        var result = new List<(Guid, decimal)>();
        var left = Math.Abs(net);
        for (var i = 0; i < other.Count; i++)
        {
            var share = i == other.Count - 1 ? left : Math.Round(Math.Abs(net) * other[i].Amount / total, 2, MidpointRounding.AwayFromZero);
            left -= share;
            result.Add((other[i].AccountId, net > 0 ? share : -share));
        }
        return result;
    }
}

internal sealed class CashFlowHandler(IReadRepository<JournalEntry> journals, IReadRepository<Account> accounts, IReadRepository<LegalEntity> entities, IReportCurrencyGuard currencies, ICurrentUser user) :
    IRequestHandler<GetCashFlowStatement, CashFlowStatementDto>
{
    public async Task<CashFlowStatementDto> Handle(GetCashFlowStatement q, CancellationToken ct)
    {
        await currencies.EnsureSingleCurrencyAsync(q.LegalEntityId, ct);
        var entity = q.LegalEntityId;
        var chart = await accounts.Query().Select(a => new { a.Id, a.Code, a.Name, a.Type, a.ReportingGroup, a.SystemRole, a.IsBank }).ToListAsync(ct);
        // Inactive accounts count: their history is still in the balance the statement has to tie out to.
        var cash = chart.Where(a => a.Type == AccountType.Asset && (a.IsBank || a.SystemRole is "bank" or "cash")).Select(a => a.Id).ToHashSet();
        var cashIds = cash.ToList();

        var cashLines = await journals.Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.Date <= q.To && (entity == null || j.LegalEntityId == entity)))
            .SelectMany(j => j.Lines.Where(l => cashIds.Contains(l.AccountId)), (j, l) => new { j.Date, j.SourceType, l.Debit, l.Credit }).ToListAsync(ct);
        // An opening entry is not a flow: what it brings in is cash the entity already had, whatever date it carries.
        var opening = cashLines.Where(l => l.Date < q.From || l.SourceType == LedgerSources.Opening).Sum(l => l.Debit - l.Credit);
        var ledgerClosing = cashLines.Sum(l => l.Debit - l.Credit);

        var entries = await journals.Query(new Specification<JournalEntry>(j => j.Status != JournalStatus.Draft && j.SourceType != LedgerSources.Opening && j.Date >= q.From && j.Date <= q.To
                && (entity == null || j.LegalEntityId == entity) && j.Lines.Any(l => cashIds.Contains(l.AccountId))))
            .SelectMany(j => j.Lines, (j, l) => new { j.Id, l.AccountId, l.Debit, l.Credit }).ToListAsync(ct);

        var flows = new Dictionary<Guid, (decimal In, decimal Out)>();
        var transfers = 0m;
        foreach (var journal in entries.GroupBy(e => e.Id))
        {
            var lines = journal.Select(l => new CashFlowAllocator.EntryLine(l.AccountId, l.Debit, l.Credit)).ToList();
            transfers += Math.Min(lines.Where(l => cash.Contains(l.AccountId)).Sum(l => l.Debit), lines.Where(l => cash.Contains(l.AccountId)).Sum(l => l.Credit));
            foreach (var (accountId, amount) in CashFlowAllocator.Allocate(lines, cash))
            {
                var f = flows.GetValueOrDefault(accountId);
                flows[accountId] = amount > 0 ? (f.In + amount, f.Out) : (f.In, f.Out - amount);
            }
        }

        var byId = chart.ToDictionary(a => a.Id);
        var sections = Enum.GetValues<CashFlowCategory>().Select(category =>
        {
            var lines = flows.Where(f => CashFlowClassifier.Classify(byId[f.Key].Type, byId[f.Key].ReportingGroup, byId[f.Key].SystemRole) == category)
                .Select(f => new CashFlowLine(byId[f.Key].ReportingGroup ?? byId[f.Key].Type.ToString(), byId[f.Key].Code, byId[f.Key].Name, f.Value.In, f.Value.Out, f.Value.In - f.Value.Out))
                .OrderBy(l => l.Code).ToList();
            var (inflows, outflows) = (lines.Sum(l => l.Inflows), lines.Sum(l => l.Outflows));
            return new CashFlowSection(category, lines, inflows, outflows, inflows - outflows);
        }).ToList();

        var net = sections.Sum(s => s.Net);
        var closing = opening + net;
        var scope = entity ?? user.LegalEntityId;
        var currency = scope is { } one ? (await entities.FindAsync(one, ct))?.BaseCurrency : null;
        return new CashFlowStatementDto(q.From, q.To, q.LegalEntityId, currency, opening, sections, net, closing, ledgerClosing, closing == ledgerClosing, transfers);
    }
}
