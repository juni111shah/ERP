using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class ClearIssuedChequeHandler(
    IPaymentInstrumentRepository instruments,
    ISettlementRepository settlements,
    IPostingService posting,
    IClock clock,
    IUnitOfWork uow,
    PaymentDtoBuilder dtos)
    : ICommandHandler<ClearIssuedChequeCommand, PaymentDto>
{
    public Task<PaymentDto> Handle(ClearIssuedChequeCommand command, CancellationToken ct) => uow.InTransactionAsync(token => ClearAsync(command, token), ct);

    private async Task<PaymentDto> ClearAsync(ClearIssuedChequeCommand command, CancellationToken ct)
    {
        // 1. Only an issued cheque that has not cleared, on a date between its issue and today.
        var c = await instruments.GetIssuedForUpdateAsync(command.InstrumentId, ct);
        if (c.Status != InstrumentStatus.Issued) throw new ConflictException("Only an issued cheque that has not cleared can be cleared.");
        if (c.ReceivedOn is { } issueDate && command.Date < issueDate) throw new FieldValidationException("date", "The clearing date cannot be before the cheque was issued.");
        if (command.Date > DateOnly.FromDateTime(clock.UtcNow)) throw new FieldValidationException("date", "The clearing date cannot be in the future.");

        // 2. Post it (Dr cheques issued, Cr bank), mark it cleared and save.
        var memo = $"Cheque {c.InstrumentNo} cleared";
        var issued = await posting.AccountForRoleAsync("instruments.issued", ct);
        await posting.PostAsync(new PostingRequest(c.LegalEntityId, command.Date, "cheque", c.Id, memo, c.Number,
            [new PostingLine(issued, c.Amount, 0, memo, c.PartyId), new PostingLine(c.BankAccountId!.Value, 0, c.Amount, memo)], c.CurrencyCode), ct);
        c.ClearedOn = command.Date; c.Status = InstrumentStatus.Cleared;
        await instruments.SaveChangesAsync(ct);

        // 3. Answer with the payment the cheque belongs to.
        return await dtos.BuildAsync(await settlements.GetPaymentWithAllocationsForUpdateAsync(c.SettlementId!.Value, ct), ct);
    }
}
