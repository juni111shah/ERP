using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetGeneralLedgerHandler(IJournalEntryRepository journals, IAccountRepository accounts, IReportCurrencyGuard currencies)
    : IQueryHandler<GetGeneralLedgerQuery, GeneralLedgerDto>
{
    public async Task<GeneralLedgerDto> Handle(GetGeneralLedgerQuery q, CancellationToken ct)
    {
        // 1. One currency, one account, every posted line up to the end date.
        await currencies.EnsureSingleCurrencyAsync(q.LegalEntityId, ct);
        var account = await accounts.GetAsync(q.AccountId, ct);
        var lines = await journals.ListPostedLinesOfAccountAsync(q.AccountId, q.To, q.LegalEntityId, ct);

        // 2. Opening balance before the start date, then a running balance through the period.
        var opening = q.From is { } from ? lines.Where(l => l.Date < from).Sum(l => l.Debit - l.Credit) : 0m;
        var running = opening;
        var inRange = new List<LedgerLineDto>();
        foreach (var l in lines.Where(l => q.From is not { } f || l.Date >= f).OrderBy(l => l.Date).ThenBy(l => l.Number).ThenBy(l => l.Seq))
        {
            running += l.Debit - l.Credit;
            inRange.Add(new LedgerLineDto(l.JournalId, l.Number, l.Date, l.Memo, l.Description, l.Debit, l.Credit, running));
        }
        return new GeneralLedgerDto(account.Id, account.Code, account.Name, q.From, q.To, opening, inRange, running);
    }
}
