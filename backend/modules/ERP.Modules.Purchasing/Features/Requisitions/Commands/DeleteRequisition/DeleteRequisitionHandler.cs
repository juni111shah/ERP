using ERP.Application.Common;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;
using MediatR;

namespace ERP.Modules.Purchasing.Features;

internal sealed class DeleteRequisitionHandler(IPurchaseRequisitionRepository requisitions, PurchasingApprovalGate approvalGate) : ICommandHandler<DeleteRequisitionCommand>
{
    public async Task<Unit> Handle(DeleteRequisitionCommand cmd, CancellationToken ct)
    {
        var req = await requisitions.GetForUpdateAsync(cmd.Id, ct);
        if (req.Status == PurchaseRequisitionStatus.Ordered)
            throw new ConflictException("Cannot delete an ordered requisition.");

        await approvalGate.ReleaseAsync(req.ApprovalRequestId, ct);
        requisitions.Remove(req);
        await requisitions.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
