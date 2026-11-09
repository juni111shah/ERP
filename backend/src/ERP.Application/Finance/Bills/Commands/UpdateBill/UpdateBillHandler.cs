using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class UpdateBillHandler(IBillRepository bills, IRepository<BillLine> lineRows, BillDrafts drafts, IUnitOfWork uow) : ICommandHandler<UpdateBillCommand, BillDto>
{
    public Task<BillDto> Handle(UpdateBillCommand command, CancellationToken ct) => uow.InTransactionAsync(token => UpdateAsync(command, token), ct);

    private async Task<BillDto> UpdateAsync(UpdateBillCommand command, CancellationToken ct)
    {
        // 1. Only a draft (or pending) bill can change, and never its legal entity.
        var bill = await bills.GetWithLinesForUpdateAsync(command.Id, ct);
        if (bill.Status is not (DocumentStatus.Draft or DocumentStatus.PendingApproval)) throw new ConflictException("Only a draft bill can be edited. Void it instead.");
        if (command.Input.LegalEntityId != bill.LegalEntityId) throw new FieldValidationException("legalEntityId", "The legal entity of a bill cannot change.");

        // 2. Re-price from the new input and swap the lines.
        var fresh = await drafts.ApplyAsync(bill, command.Input, ct);
        foreach (var old in bill.Lines.ToList()) lineRows.Remove(old);
        lineRows.AddRange(fresh);

        // 3. An edit invalidates a pending approval request: back to draft, then save.
        await drafts.WithdrawPendingAsync(bill, ct);
        bill.Status = DocumentStatus.Draft; bill.ApprovalRequestId = null;
        await drafts.SaveAsync(bill, ct);
        bill.Lines.Clear(); bill.Lines.AddRange(fresh);
        return BillDto.From(bill);
    }
}
