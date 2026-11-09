using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using ERP.Domain.Platform;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Manufacturing.Repositories;

namespace ERP.Modules.Manufacturing.Features;

internal sealed class ReverseWorkOrderHandler(
    IWorkOrderRepository orders,
    WorkOrderMaterials materials,
    IStockPostingService stockPosting,
    IJournalEntryRepository journals,
    IRepository<AuditEvent> audit,
    ICurrentUser user,
    IClock clock,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReverseWorkOrderCommand, WorkOrderDto>
{
    public Task<WorkOrderDto> Handle(ReverseWorkOrderCommand request, CancellationToken cancellationToken) => unitOfWork.InTransactionAsync(async token =>
    {
        // 1. Load it and build the memo.
        var wo = await orders.GetWithDetailsForUpdateAsync(request.Id, token);
        ManufacturingTransitions.WorkOrder.Ensure(wo.Status, WorkOrderStatus.Reversed, isSystem: false);
        var reason = request.Reason.Trim();
        var memo = $"Reversal of WO {wo.WorkOrderNumber}: {reason}";
        if (memo.Length > VoidMemo.MaxMemo) memo = memo[..(VoidMemo.MaxMemo - 3)] + "...";

        // 2. Take the finished goods back.
        if (await stockPosting.IsInventoryEnabledAsync(token))
        {
            // The stock service takes the receipt back only if on-hand still covers it. An order completed with Inventory off has no receipt, and this does nothing.
            try { await stockPosting.ReverseReceiptAsync(wo.WorkOrderNumber, token); }
            catch (ConflictException)
            {
                throw new ConflictException($"Some of the {wo.CompletedQuantity:0.####} {wo.ProducedItemCode} this order produced have left stock, so it can no longer be reversed.");
            }
        }
        else if (await journals.AnyAsync(j => j.SourceType == WorkOrderRules.JournalSource && j.SourceId == wo.Id && j.Status == JournalStatus.Posted, token))
        {
            throw new ConflictException("Turn the Inventory module on first: this work order moved stock that has to go back.");
        }

        // 3. Put the material back and reverse the journals.
        await materials.UndoMaterialAsync(wo, memo, token);
        await materials.ReverseJournalsAsync(wo, memo, token);

        // 4. Mark it reversed, with an audit entry.
        wo.Status = WorkOrderStatus.Reversed;
        wo.ReversedAt = clock.UtcNow;
        wo.ReversedByUserId = user.UserId;
        wo.ReversalReason = reason;
        audit.Add(new AuditEvent
        {
            OccurredAt = clock.UtcNow,
            UserId = user.UserId,
            Action = "Reversed",
            RecordType = nameof(WorkOrder),
            RecordId = wo.Id.ToString(),
            ChangedFields = "Status",
            After = System.Text.Json.JsonSerializer.Serialize(new { number = wo.WorkOrderNumber, reason }),
            IpAddress = user.IpAddress,
        });

        // 5. Save.
        await orders.SaveChangesAsync(token);
        return WorkOrderDto.From(wo);
    }, cancellationToken);
}
