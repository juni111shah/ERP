using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Finance;
using ERP.Domain.Platform;
using MediatR;

namespace ERP.Application.Finance;

internal sealed class VoidBillHandler(
    IBillRepository bills,
    IJournalEntryRepository journals,
    ILegalEntityGuard guard,
    IPostingService posting,
    IRepository<AuditEvent> audit,
    ICurrentUser user,
    IClock clock,
    IUnitOfWork uow,
    IPublisher publisher)
    : ICommandHandler<VoidBillCommand, BillDto>
{
    public Task<BillDto> Handle(VoidBillCommand command, CancellationToken ct) => uow.InTransactionAsync(token => VoidAsync(command, token), ct);

    private async Task<BillDto> VoidAsync(VoidBillCommand command, CancellationToken ct)
    {
        // 1. Only a posted bill with nothing paid, and not one from the opening entry.
        var bill = await bills.GetWithLinesForUpdateAsync(command.Id, ct);
        if (bill.Status != DocumentStatus.Issued || bill.Paid != 0) throw new ConflictException("Only a posted bill with nothing paid can be voided.");
        if (bill.SourceType == LedgerSources.Opening) throw new ConflictException("This document comes from the opening entry. Reverse the opening entry instead.");

        // 2. Reverse the journal.
        var journal = await journals.GetWithLinesForUpdateAsync(bill.JournalEntryId, ct);
        var lockedTo = (await guard.RequireAsync(bill.LegalEntityId, ct)).BooksLockedTo;
        await posting.ReverseTodayAsync(journal, VoidMemo.For(bill.Number, command.Reason), ct);

        // 3. Mark it voided, audit, release what it was raised from and save.
        bill.Status = DocumentStatus.Voided;
        VoidMemo.Stamp(bill, command.Reason, clock, user);
        VoidMemo.MarkIfLocked(bill, bill.BillDate, lockedTo, clock);
        VoidMemo.Audit(audit, user, clock, nameof(Bill), bill.Id, bill.Number, command.Reason!);
        await publisher.Publish(new BillReleased(bill.Id), ct);
        await bills.SaveChangesAsync(ct);
        return BillDto.From(bill);
    }
}
