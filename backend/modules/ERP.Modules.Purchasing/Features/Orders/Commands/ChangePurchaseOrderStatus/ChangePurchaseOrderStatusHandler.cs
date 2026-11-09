using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

internal sealed class ChangePurchaseOrderStatusHandler(
    IPurchaseOrderRepository orders,
    PurchaseOrderRules rules,
    PurchasingApprovalGate approvalGate,
    IUnitOfWork unitOfWork,
    ICustomFieldService fields) : ICommandHandler<ChangePurchaseOrderStatusCommand, PurchaseOrderDto>
{
    public Task<PurchaseOrderDto> Handle(ChangePurchaseOrderStatusCommand cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(token => ChangeStatusAsync(cmd, token), ct);

    private async Task<PurchaseOrderDto> ChangeStatusAsync(ChangePurchaseOrderStatusCommand cmd, CancellationToken ct)
    {
        // 1. The move must be allowed, and an order with bills cannot be cancelled.
        var order = await orders.FindWithLinesForUpdateAsync(cmd.Id, ct) ?? throw new NotFoundException($"Purchase order '{cmd.Id}' not found.");

        PurchasingTransitions.Order.Ensure(order.Status, cmd.Status, isSystem: false);
        if (cmd.Status == PurchaseOrderStatus.Cancelled && order.Lines.Any(l => l.BilledQuantity > 0))
            throw new ConflictException("This purchase order has bills. Void or delete them before cancelling it.");

        // 2. Submitting or approving goes through the approval gate, which may hold the order at submitted or approve it at once.
        var target = cmd.Status;
        if (target is PurchaseOrderStatus.Submitted or PurchaseOrderStatus.Approved)
        {
            var gate = await approvalGate.CheckAsync(Docs.PurchaseOrder, order.Id, $"Purchase order {order.Number} from {order.VendorName}", order.TotalAmount, order.LegalEntityId,
                order.ApprovalRequestId, order.CreatedBy, approving: target == PurchaseOrderStatus.Approved, ct);
            order.ApprovalRequestId = gate.RequestId;
            if (!gate.MayApprove) target = PurchaseOrderStatus.Submitted;
            else if (gate.AutoApproved) target = PurchaseOrderStatus.Approved;
        }

        // 3. A cancelled order gives back its requisition and approval.
        order.Status = target;
        if (cmd.Status == PurchaseOrderStatus.Cancelled)
        {
            await rules.ReleaseRequisitionAsync(order, ct);
            await approvalGate.ReleaseAsync(order.ApprovalRequestId, ct);
        }
        await orders.SaveChangesAsync(ct);
        return await PurchaseOrderDto.FromAsync(order, fields, ct);
    }
}
