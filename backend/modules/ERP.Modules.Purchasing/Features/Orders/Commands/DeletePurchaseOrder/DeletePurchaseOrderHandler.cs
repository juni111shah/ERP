using ERP.Application.Common;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;
using MediatR;

namespace ERP.Modules.Purchasing.Features;

internal sealed class DeletePurchaseOrderHandler(
    IPurchaseOrderRepository orders,
    IGoodsReceiptRepository goodsReceipts,
    PurchaseOrderRules rules,
    PurchasingApprovalGate approvalGate) : ICommandHandler<DeletePurchaseOrderCommand>
{
    public async Task<Unit> Handle(DeletePurchaseOrderCommand cmd, CancellationToken ct)
    {
        // 1. Only a draft or cancelled order with no receipts can go.
        var order = await orders.GetForUpdateAsync(cmd.Id, ct);
        if (order.Status == PurchaseOrderStatus.Billed)
            throw new ConflictException("Cannot delete a billed purchase order.");
        if (order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Cancelled))
            throw new ConflictException("Only a draft or cancelled purchase order can be deleted.");
        if (await goodsReceipts.AnyAsync(x => x.PurchaseOrderId == order.Id, ct))
            throw new ConflictException("This purchase order has goods receipts and cannot be deleted.");

        // 2. Give back the requisition and the approval, then delete.
        await rules.ReleaseRequisitionAsync(order, ct);
        await approvalGate.ReleaseAsync(order.ApprovalRequestId, ct);
        orders.Remove(order);
        await orders.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
