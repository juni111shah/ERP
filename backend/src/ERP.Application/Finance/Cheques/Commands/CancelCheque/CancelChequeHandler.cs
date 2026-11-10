using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class CancelChequeHandler(
    IPaymentInstrumentRepository instruments,
    ISettlementRepository settlements,
    ISettlementService settlementService,
    ILegalEntityGuard guard,
    IClock clock,
    ICurrentUser user,
    ChequeDtoBuilder dtos)
    : ICommandHandler<CancelChequeCommand, ChequeDto>
{
    public async Task<ChequeDto> Handle(CancelChequeCommand command, CancellationToken ct)
    {
        // 1. Only a cheque still in custody.
        var c = await instruments.GetForUpdateAsync(command.Id, ct);
        if (c.Status != InstrumentStatus.InCustody) throw new ConflictException("Only a cheque still in custody can be cancelled.");

        // 2. Reverse its receipt.
        var s = await settlements.GetWithAllocationsForUpdateAsync(c.SettlementId, ct);
        await settlementService.ReverseAsync(s, $"Cancel {c.Number}", ct);
        s.Status = DocumentStatus.Voided;
        VoidMemo.Stamp(s, $"Cheque {c.InstrumentNo} cancelled", clock, user);
        VoidMemo.MarkIfLocked(s, s.Date, (await guard.RequireAsync(s.LegalEntityId, ct)).BooksLockedTo, clock);

        // 3. Mark the cheque cancelled and save.
        c.Status = InstrumentStatus.Cancelled;
        await instruments.SaveChangesAsync(ct);
        return await dtos.BuildAsync(c, ct);
    }
}
