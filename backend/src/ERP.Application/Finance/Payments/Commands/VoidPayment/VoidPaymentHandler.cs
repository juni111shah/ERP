using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Finance;
using ERP.Domain.Platform;

namespace ERP.Application.Finance;

internal sealed class VoidPaymentHandler(
    ISettlementRepository settlements,
    IBillRepository bills,
    IPaymentInstrumentRepository instruments,
    IJournalEntryRepository journals,
    ILegalEntityGuard guard,
    IPostingService posting,
    IRepository<AuditEvent> audit,
    ICurrentUser user,
    IClock clock,
    IUnitOfWork uow,
    PaymentDtoBuilder dtos)
    : ICommandHandler<VoidPaymentCommand, PaymentDto>
{
    public Task<PaymentDto> Handle(VoidPaymentCommand command, CancellationToken ct) => uow.InTransactionAsync(token => VoidAsync(command, token), ct);

    private async Task<PaymentDto> VoidAsync(VoidPaymentCommand command, CancellationToken ct)
    {
        // 1. Only a posted payment whose cheque (if any) has not cleared can be voided.
        var s = await settlements.GetPaymentWithAllocationsForUpdateAsync(command.Id, ct);
        if (s.Status == DocumentStatus.PendingApproval) throw new ConflictException("This payment has not been posted. Delete it instead.");
        if (s.Status != DocumentStatus.Issued) throw new ConflictException("This payment is already voided.");
        PaymentInstrument? cheque = null;
        if (s.PaymentInstrumentId is { } iid)
        {
            cheque = await instruments.FindForUpdateAsync(iid, ct);
            if (cheque?.Status == InstrumentStatus.Cleared) throw new ConflictException("The cheque has already cleared the bank, so the payment cannot be voided.");
        }

        // 2. Reverse the journal and take the allocations back off the bills.
        var journal = await journals.GetWithLinesForUpdateAsync(s.JournalEntryId, ct);
        var lockedTo = (await guard.RequireAsync(s.LegalEntityId, ct)).BooksLockedTo;
        await posting.ReverseTodayAsync(journal, VoidMemo.For(s.Number, command.Reason), ct);
        VoidMemo.MarkIfLocked(s, s.Date, lockedTo, clock);
        var ids = s.Allocations.Select(a => a.BillId!.Value).ToList();
        foreach (var bill in await bills.ListByIdsForUpdateAsync(ids, ct))
        {
            bill.Paid -= s.Allocations.Where(a => a.BillId == bill.Id).Sum(a => a.Amount);
            BillStatus.Refresh(bill);
        }

        // 3. Mark it (and its cheque) voided, audit and save.
        s.Status = DocumentStatus.Voided; s.Unallocated = 0;
        VoidMemo.Stamp(s, command.Reason, clock, user);
        if (cheque is not null) cheque.Status = InstrumentStatus.Cancelled;
        VoidMemo.Audit(audit, user, clock, nameof(Settlement), s.Id, s.Number, command.Reason!);
        await settlements.SaveChangesAsync(ct);
        return await dtos.BuildAsync(s, ct);
    }
}
