using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class ClearChequeHandler(IPaymentInstrumentRepository instruments, IPostingService posting, ChequeDtoBuilder dtos) : ICommandHandler<ClearChequeCommand, ChequeDto>
{
    public async Task<ChequeDto> Handle(ClearChequeCommand command, CancellationToken ct)
    {
        // 1. Only a deposited cheque, not before it was deposited.
        var c = await instruments.GetForUpdateAsync(command.Id, ct);
        if (c.Status != InstrumentStatus.Deposited) throw new ConflictException("Only a deposited cheque can be cleared.");
        if (command.Date < c.DepositedOn) throw new FieldValidationException("date", "A cheque cannot clear before it was deposited.");

        // 2. Post it (Dr bank, Cr cheques in hand), mark it cleared and save.
        var memo = $"Cheque {c.InstrumentNo} cleared";
        var held = await posting.AccountForRoleAsync("instruments.received", ct);
        await posting.PostAsync(new PostingRequest(c.LegalEntityId, command.Date, "cheque", c.Id, memo, c.Number,
            [new PostingLine(c.BankAccountId!.Value, c.Amount, 0, memo), new PostingLine(held, 0, c.Amount, memo, c.PartyId)], c.CurrencyCode), ct);
        c.ClearedOn = command.Date; c.Status = InstrumentStatus.Cleared;
        await instruments.SaveChangesAsync(ct);
        return await dtos.BuildAsync(c, ct);
    }
}
