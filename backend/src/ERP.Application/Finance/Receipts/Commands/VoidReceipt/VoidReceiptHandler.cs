using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Finance;
using ERP.Domain.Platform;

namespace ERP.Application.Finance;

internal sealed class VoidReceiptHandler(
    ISettlementRepository settlements,
    ISettlementService settlementService,
    ILegalEntityGuard guard,
    IRepository<AuditEvent> audit,
    ICurrentUser user,
    IClock clock,
    ReceiptDtoBuilder dtos)
    : ICommandHandler<VoidReceiptCommand, ReceiptDto>
{
    public async Task<ReceiptDto> Handle(VoidReceiptCommand command, CancellationToken ct)
    {
        // 1. Only a posted receipt that is not a registered cheque can be voided here.
        var s = await settlements.GetReceiptWithAllocationsForUpdateAsync(command.Id, ct);
        if (s.Status != DocumentStatus.Issued) throw new ConflictException("This receipt is already voided.");
        if (s.PaymentInstrumentId is not null) throw new ConflictException("This receipt belongs to a cheque in the register. Cancel or bounce the cheque there instead.");

        // 2. Reverse the journal and the allocations.
        var lockedTo = (await guard.RequireAsync(s.LegalEntityId, ct)).BooksLockedTo;
        await settlementService.ReverseAsync(s, VoidMemo.For(s.Number, command.Reason), ct);

        // 3. Mark it voided, audit and save.
        s.Status = DocumentStatus.Voided; s.Unallocated = 0;
        VoidMemo.Stamp(s, command.Reason, clock, user);
        VoidMemo.MarkIfLocked(s, s.Date, lockedTo, clock);
        VoidMemo.Audit(audit, user, clock, nameof(Settlement), s.Id, s.Number, command.Reason!);
        await settlements.SaveChangesAsync(ct);
        return await dtos.BuildAsync(s, ct);
    }
}
