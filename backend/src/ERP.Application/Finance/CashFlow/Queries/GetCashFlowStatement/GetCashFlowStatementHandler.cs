using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using ERP.Domain.Organizations;

namespace ERP.Application.Finance;

internal sealed class GetCashFlowStatementHandler(
    IJournalEntryRepository journals, IAccountRepository accounts, IReadRepository<LegalEntity> entities, IReportCurrencyGuard currencies, ICurrentUser user)
    : IQueryHandler<GetCashFlowStatementQuery, CashFlowStatementDto>
{
    public async Task<CashFlowStatementDto> Handle(GetCashFlowStatementQuery q, CancellationToken ct)
    {
        // 1. The bank and cash accounts.
        await currencies.EnsureSingleCurrencyAsync(q.LegalEntityId, ct);
        var entity = q.LegalEntityId;
        var chart = await accounts.ListCashFlowChartAsync(ct);
        // Inactive accounts count: their history is still in the balance the statement has to tie out to.
        var cash = chart.Where(a => a.Type == AccountType.Asset && (a.IsBank || a.SystemRole is "bank" or "cash")).Select(a => a.Id).ToHashSet();
        var cashIds = cash.ToList();

        // 2. Opening and closing cash from the ledger.
        var cashLines = await journals.ListPostedCashLinesAsync(cashIds, q.To, entity, ct);
        // An opening entry is not a flow: what it brings in is cash the entity already had, whatever date it carries.
        var opening = cashLines.Where(l => l.Date < q.From || l.SourceType == LedgerSources.Opening).Sum(l => l.Debit - l.Credit);
        var ledgerClosing = cashLines.Sum(l => l.Debit - l.Credit);

        // 3. Each journal of the period that moved cash: its cash split over the other side of the entry.
        var entries = await journals.ListLinesOfCashJournalsAsync(cashIds, q.From, q.To, entity, ct);
        var flows = new Dictionary<Guid, (decimal In, decimal Out)>();
        var transfers = 0m;
        foreach (var journal in entries.GroupBy(e => e.JournalId))
        {
            var lines = journal.Select(l => new CashFlowAllocator.EntryLine(l.AccountId, l.Debit, l.Credit)).ToList();
            transfers += Math.Min(lines.Where(l => cash.Contains(l.AccountId)).Sum(l => l.Debit), lines.Where(l => cash.Contains(l.AccountId)).Sum(l => l.Credit));
            foreach (var (accountId, amount) in CashFlowAllocator.Allocate(lines, cash))
            {
                var f = flows.GetValueOrDefault(accountId);
                flows[accountId] = amount > 0 ? (f.In + amount, f.Out) : (f.In, f.Out - amount);
            }
        }

        // 4. One section per category, by account.
        var byId = chart.ToDictionary(a => a.Id);
        var sections = Enum.GetValues<CashFlowCategory>().Select(category =>
        {
            var lines = flows.Where(f => CashFlowClassifier.Classify(byId[f.Key].Type, byId[f.Key].ReportingGroup, byId[f.Key].SystemRole) == category)
                .Select(f => new CashFlowLine(byId[f.Key].ReportingGroup ?? byId[f.Key].Type.ToString(), byId[f.Key].Code, byId[f.Key].Name, f.Value.In, f.Value.Out, f.Value.In - f.Value.Out))
                .OrderBy(l => l.Code).ToList();
            var (inflows, outflows) = (lines.Sum(l => l.Inflows), lines.Sum(l => l.Outflows));
            return new CashFlowSection(category, lines, inflows, outflows, inflows - outflows);
        }).ToList();

        // 5. Totals, in the entity's currency when there is one.
        var net = sections.Sum(s => s.Net);
        var closing = opening + net;
        var scope = entity ?? user.LegalEntityId;
        var currency = scope is { } one ? (await entities.FindAsync(one, ct))?.BaseCurrency : null;
        return new CashFlowStatementDto(q.From, q.To, q.LegalEntityId, currency, opening, sections, net, closing, ledgerClosing, closing == ledgerClosing, transfers);
    }
}
