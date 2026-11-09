using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class GetReconciliationHandler(IBankStatementLineRepository lines, IJournalEntryRepository journals, IReadRepository<Account> accounts)
    : IQueryHandler<GetReconciliationQuery, ReconciliationDto>
{
    public async Task<ReconciliationDto> Handle(GetReconciliationQuery query, CancellationToken ct)
    {
        // 1. The account's ledger lines and which of them are matched.
        var account = await accounts.GetAsync(query.AccountId, ct);
        var all = await journals.ListAccountLinesAsync(query.AccountId, query.LegalEntityId, query.AsOf, ct);
        var matchedIds = await lines.MatchedJournalLineIdsAsync(query.AccountId, ct);

        // 2. What is unmatched on each side.
        var statementOpen = await lines.ListUnmatchedAsOfAsync(query.AccountId, query.AsOf, query.LegalEntityId, ct);
        var ledgerOpen = all.Where(x => !matchedIds.Contains(x.JournalLineId)).Select(BankLedger.ToItem).ToList();
        var matchedCount = await lines.CountAsync(l => l.AccountId == query.AccountId && l.Status == StatementLineStatus.Matched, ct);

        // 3. Reconciled when nothing is left on either side.
        var stTotal = statementOpen.Sum(l => l.Amount); var ledTotal = ledgerOpen.Sum(l => l.Amount);
        return new ReconciliationDto(account.Id, account.Code, account.Name, query.AsOf, all.Sum(x => x.Debit - x.Credit), matchedCount, statementOpen.Select(StatementLineDto.From).ToList(), ledgerOpen, stTotal, ledTotal, statementOpen.Count == 0 && ledgerOpen.Count == 0);
    }
}
