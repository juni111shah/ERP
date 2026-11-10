using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class ReverseOpeningBalanceHandler(
    IJournalEntryRepository journals, OpeningBalanceDocuments documents, IPostingService posting, IUnitOfWork uow, ICurrentUser user, IClock clock)
    : ICommandHandler<ReverseOpeningBalanceCommand, OpeningBalanceDto>
{
    public Task<OpeningBalanceDto> Handle(ReverseOpeningBalanceCommand cmd, CancellationToken ct) => uow.InTransactionAsync(token => ReverseAsync(cmd, token), ct);

    private async Task<OpeningBalanceDto> ReverseAsync(ReverseOpeningBalanceCommand cmd, CancellationToken ct)
    {
        // 1. A standing opening entry whose documents are all unsettled.
        var journal = await journals.FindOpeningEntryForUpdateAsync(cmd.JournalId, ct) ?? throw new NotFoundException("Opening entry not found.");
        if (journal.Status != JournalStatus.Posted) throw new ConflictException("This opening entry has already been reversed.");
        var openInvoices = await documents.ListInvoicesForUpdateAsync(journal.Id, ct);
        var openBills = await documents.ListBillsForUpdateAsync(journal.Id, ct);
        if (openInvoices.Any(d => d.Paid != 0) || openBills.Any(d => d.Paid != 0))
            throw new ConflictException("Some opening documents have been settled. Void those receipts and payments first.");

        // 2. Reverse the entry, void its documents and save.
        var reason = cmd.Reason!.Trim();
        var reversal = await posting.ReverseAsync(journal, cmd.Date ?? journal.Date, VoidMemo.For(journal.Number, reason), ct);
        reversal.SourceType = LedgerSources.Opening; // keeps its cash out of the cash flow statement, as the entry it undoes
        foreach (var d in openInvoices) { d.Status = DocumentStatus.Voided; VoidMemo.Stamp(d, reason, clock, user); }
        foreach (var d in openBills) { d.Status = DocumentStatus.Voided; VoidMemo.Stamp(d, reason, clock, user); }
        await uow.SaveChangesAsync(ct);

        // 3. Answer with what the entry put on opening balance equity.
        var equity = await posting.AccountForRoleAsync(SystemRoles.OpeningEquity, ct);
        return new OpeningBalanceDto(journal.Id, journal.Number, journal.LegalEntityId, journal.Date, journal.Status, journal.Amount, journal.Lines.Where(l => l.AccountId == equity).Sum(l => l.Credit - l.Debit),
            openInvoices.Select(d => d.Id).ToList(), openBills.Select(d => d.Id).ToList(), reversal.Id);
    }
}
