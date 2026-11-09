using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using MediatR;

namespace ERP.Application.Finance;

internal sealed class DeleteBillHandler(IBillRepository bills, BillDrafts drafts, IUnitOfWork uow, IPublisher publisher) : ICommandHandler<DeleteBillCommand>
{
    public Task<Unit> Handle(DeleteBillCommand command, CancellationToken ct) => uow.InTransactionAsync(token => DeleteAsync(command, token), ct);

    private async Task<Unit> DeleteAsync(DeleteBillCommand command, CancellationToken ct)
    {
        // 1. Only a draft (or pending) bill can be deleted.
        var bill = await bills.GetWithLinesForUpdateAsync(command.Id, ct);
        if (bill.Status is not (DocumentStatus.Draft or DocumentStatus.PendingApproval)) throw new ConflictException("Only a draft bill can be deleted.");

        // 2. Withdraw a pending approval request and release what the bill was raised from.
        await drafts.WithdrawPendingAsync(bill, ct);
        await publisher.Publish(new BillReleased(bill.Id), ct);

        // 3. Remove it.
        bills.Remove(bill);
        await bills.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
