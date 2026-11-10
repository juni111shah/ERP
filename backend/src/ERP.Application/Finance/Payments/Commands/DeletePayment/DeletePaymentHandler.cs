using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using ERP.Domain.Platform;
using MediatR;

namespace ERP.Application.Finance;

internal sealed class DeletePaymentHandler(
    ISettlementRepository settlements,
    IApprovalService approvals,
    IReadRepository<ApprovalRequest> approvalRequests,
    IUnitOfWork uow)
    : ICommandHandler<DeletePaymentCommand>
{
    public Task<Unit> Handle(DeletePaymentCommand command, CancellationToken ct) => uow.InTransactionAsync(token => DeleteAsync(command, token), ct);

    private async Task<Unit> DeleteAsync(DeletePaymentCommand command, CancellationToken ct)
    {
        // 1. Only a payment that never reached the ledger can be deleted.
        var s = await settlements.GetPaymentWithAllocationsForUpdateAsync(command.Id, ct);
        if (s.Status != DocumentStatus.PendingApproval) throw new ConflictException("Only a payment waiting for approval can be deleted. Void a posted one instead.");

        // 2. Withdraw its approval request while that is still pending, then remove it.
        if (s.ApprovalRequestId is { } rid && (await approvalRequests.FindAsync(rid, ct))?.Status == ApprovalStatus.Pending) await approvals.WithdrawAsync(rid, ct);
        settlements.Remove(s);
        await settlements.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
