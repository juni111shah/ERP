using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

internal sealed class UpdateRequisitionHandler(
    IPurchaseRequisitionRepository requisitions,
    ICustomFieldService fields,
    PurchasingApprovalGate approvalGate) : ICommandHandler<UpdateRequisitionCommand, RequisitionDto>
{
    public async Task<RequisitionDto> Handle(UpdateRequisitionCommand cmd, CancellationToken ct)
    {
        // 1. An ordered requisition cannot change.
        var req = await requisitions.FindWithLinesForUpdateAsync(cmd.Id, ct) ?? throw new NotFoundException($"Requisition '{cmd.Id}' not found.");

        if (req.Status == PurchaseRequisitionStatus.Ordered)
            throw new ConflictException("Cannot edit an ordered requisition.");

        // 2. The header.
        var i = cmd.Input;
        req.RequesterName = i.RequesterName.Trim();
        req.Department = i.Department?.Trim();
        req.RequisitionDate = i.RequisitionDate;
        req.RequiredDate = i.RequiredDate;
        req.Reason = i.Reason;

        // 3. What was approved is no longer what the requisition says.
        if (req.ApprovalRequestId is not null)
        {
            await approvalGate.ReleaseAsync(req.ApprovalRequestId, ct);
            req.ApprovalRequestId = null;
            if (req.Status is PurchaseRequisitionStatus.PendingApproval or PurchaseRequisitionStatus.Approved) req.Status = PurchaseRequisitionStatus.Draft;
        }

        // 4. Lines, totals and custom fields, then save.
        RequisitionLines.PopulateLinesAndTotals(req, i.Lines);
        await fields.ApplyAsync(Docs.PurchaseRequisition, req, i.CustomFields, isCreate: false, ct);

        await requisitions.SaveChangesAsync(ct);
        return await RequisitionDto.FromAsync(req, fields, ct);
    }
}
