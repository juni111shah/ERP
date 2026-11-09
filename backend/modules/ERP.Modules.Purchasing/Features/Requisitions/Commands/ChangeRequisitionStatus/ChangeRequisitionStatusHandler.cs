using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

internal sealed class ChangeRequisitionStatusHandler(
    IPurchaseRequisitionRepository requisitions,
    ICustomFieldService fields,
    PurchasingApprovalGate approvalGate,
    IUnitOfWork unitOfWork) : ICommandHandler<ChangeRequisitionStatusCommand, RequisitionDto>
{
    public Task<RequisitionDto> Handle(ChangeRequisitionStatusCommand cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(token => ChangeStatusAsync(cmd, token), ct);

    private async Task<RequisitionDto> ChangeStatusAsync(ChangeRequisitionStatusCommand cmd, CancellationToken ct)
    {
        // 1. The move must be allowed.
        var req = await requisitions.FindWithLinesForUpdateAsync(cmd.Id, ct) ?? throw new NotFoundException($"Requisition '{cmd.Id}' not found.");

        PurchasingTransitions.Requisition.Ensure(req.Status, cmd.Status, isSystem: false);

        // 2. Submitting or approving goes through the approval gate; any other move releases the approval.
        var target = cmd.Status;
        if (target is PurchaseRequisitionStatus.PendingApproval or PurchaseRequisitionStatus.Approved)
        {
            var gate = await approvalGate.CheckAsync(Docs.PurchaseRequisition, req.Id, $"Requisition {req.Number} by {req.RequesterName}", req.EstimatedTotal, req.LegalEntityId,
                req.ApprovalRequestId, req.CreatedBy, approving: target == PurchaseRequisitionStatus.Approved, ct);
            req.ApprovalRequestId = gate.RequestId;
            if (!gate.MayApprove) target = PurchaseRequisitionStatus.PendingApproval;
        }
        else
        {
            await approvalGate.ReleaseAsync(req.ApprovalRequestId, ct);
            req.ApprovalRequestId = null;
        }

        req.Status = target;
        await requisitions.SaveChangesAsync(ct);
        return await RequisitionDto.FromAsync(req, fields, ct);
    }
}
