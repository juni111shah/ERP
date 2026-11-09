using ERP.Application.Common;
using ERP.Application.Finance;

namespace ERP.Application.Printing;

internal sealed class GetPrintJournalHandler(IJournalEntryRepository journals, IAccountRepository accounts, PrintLookups lookups) : IQueryHandler<GetPrintJournalQuery, PrintJournalDto>
{
    public async Task<PrintJournalDto> Handle(GetPrintJournalQuery query, CancellationToken ct)
    {
        // 1. The entry and the company header.
        var journal = await journals.GetWithLinesAsync(query.Id, ct);
        var header = await lookups.BuildHeaderAsync(journal.LegalEntityId, ct);

        // 2. Lines in order, with the code and name of each account.
        var accountIds = journal.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accountDict = await accounts.GetLabelsAsync(accountIds, ct);
        var lines = journal.Lines.OrderBy(l => l.Seq).Select(l => new JournalLineDto(
            l.Id, l.Seq, l.AccountId,
            accountDict.TryGetValue(l.AccountId, out var acc) ? acc.Code : string.Empty,
            acc?.Name ?? "Unknown account",
            l.Debit, l.Credit, l.Description, l.PartyId)).ToList();

        var journalDto = new JournalDto(
            journal.Id, journal.Number, journal.LegalEntityId, journal.Date, journal.Memo, journal.Reference,
            journal.SourceType, journal.SourceId, journal.CurrencyCode, journal.Status, journal.Amount,
            journal.ReversalOfId, journal.PostedAt, lines);

        return new PrintJournalDto(
            header,
            journalDto,
            lines,
            journal.Status);
    }
}
