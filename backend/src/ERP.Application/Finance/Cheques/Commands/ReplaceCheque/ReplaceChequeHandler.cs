using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class ReplaceChequeHandler(
    IPaymentInstrumentRepository instruments,
    ISettlementAllocationRepository allocationRows,
    IInvoiceRepository invoices,
    ChequeRegistration registration,
    IUnitOfWork uow,
    ChequeDtoBuilder dtos)
    : ICommandHandler<ReplaceChequeCommand, ChequeDto>
{
    public Task<ChequeDto> Handle(ReplaceChequeCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Only a bounced cheque, replaced by one from the same customer.
        var old = await instruments.GetForUpdateAsync(command.Id, token);
        if (old.Status != InstrumentStatus.Bounced) throw new ConflictException("Only a bounced cheque can be replaced.");
        if (command.Replacement.PartyId != old.PartyId) throw new FieldValidationException("partyId", "The replacement must come from the same customer.");

        // 2. Without allocations, settle again what the bounced cheque had settled.
        var input = command.Replacement;
        if ((input.Allocations is null || input.Allocations.Count == 0) && !input.AutoAllocate)
            input = input with { Allocations = await RelinkAsync(old, input.Amount, token) };

        // 3. Register the replacement, mark the old one replaced and save.
        var fresh = await registration.RegisterAsync(input, old.Id, token);
        old.Status = InstrumentStatus.Replaced;
        await instruments.SaveChangesAsync(token);
        return await dtos.BuildAsync(fresh, token);
    }, ct);

    /// <summary>The invoices the bounced cheque had settled that are open again, each with what it can still take, up to the replacement's amount.</summary>
    private async Task<List<AllocationInput>> RelinkAsync(PaymentInstrument bounced, decimal amount, CancellationToken ct)
    {
        var previous = await allocationRows.ListInvoiceAllocationsAsync(bounced.SettlementId, ct);
        var ids = previous.Select(a => a.InvoiceId!.Value).ToList();
        var open = await invoices.ListOpenByDueDateAsync(ids, ct);
        var left = amount; var result = new List<AllocationInput>();
        foreach (var i in open)
        {
            var take = Math.Min(left, Math.Min(i.Outstanding, previous.Where(a => a.InvoiceId == i.Id).Sum(a => a.Amount)));
            if (take > 0) { result.Add(new AllocationInput(i.Id, take)); left -= take; }
        }
        return result;
    }
}
