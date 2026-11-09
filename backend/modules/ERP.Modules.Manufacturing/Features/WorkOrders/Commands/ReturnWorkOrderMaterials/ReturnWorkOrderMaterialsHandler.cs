using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Manufacturing.Repositories;

namespace ERP.Modules.Manufacturing.Features;

internal sealed class ReturnWorkOrderMaterialsHandler(
    IWorkOrderRepository orders,
    IRepository<WorkOrderMaterialIssue> issues,
    IStockPostingService stockPosting,
    IPostingService posting,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReturnWorkOrderMaterialsCommand, WorkOrderDto>
{
    public Task<WorkOrderDto> Handle(ReturnWorkOrderMaterialsCommand request, CancellationToken cancellationToken) => unitOfWork.InTransactionAsync(async token =>
    {
        // 1. Only material on an order in progress can come back.
        var wo = await orders.GetWithDetailsForUpdateAsync(request.WorkOrderId, token);
        if (wo.Status != WorkOrderStatus.InProgress) throw new ConflictException("Only material on a work order in progress can be returned.");
        var date = WorkOrderRules.DateOrToday(request.Date, "date");
        var memo = $"Returned from WO {wo.WorkOrderNumber}";
        var net = WorkOrderRules.NetIssued(wo);

        // 2. Receive each item back at the cost it left at, recording a negative issue.
        decimal total = 0;
        foreach (var line in request.Lines.GroupBy(l => l.ItemId))
        {
            var quantity = line.Sum(l => l.Quantity);
            if (!net.TryGetValue(line.Key, out var issued) || quantity > issued.Quantity)
                throw new ConflictException($"Only {(net.TryGetValue(line.Key, out var had) ? had.Quantity : 0m):0.####} of that item is on this work order.");

            var unitCost = Math.Round(issued.Value / issued.Quantity, 4);
            var moved = await stockPosting.ReceiveStockAsync(new StockReceiveRequest(
                wo.LegalEntityId, line.Key, quantity, unitCost, date, $"{wo.WorkOrderNumber}-RET", memo, wo.WarehouseId, SkipJournal: true), token);
            if (moved is null) throw new ConflictException("Stock is not being tracked, so nothing can be returned. Is the Inventory module on?");

            var value = quantity == issued.Quantity ? issued.Value : Math.Round(quantity * unitCost, 2); // the last unit takes what is left, so WIP lands on zero
            total += value;
            var first = wo.MaterialIssues.First(i => i.ItemId == line.Key);
            issues.Add(new WorkOrderMaterialIssue
            {
                LegalEntityId = wo.LegalEntityId,
                WorkOrderId = wo.Id,
                ItemId = line.Key,
                ItemCode = first.ItemCode,
                ItemName = first.ItemName,
                Quantity = -quantity,
                UnitCost = unitCost,
                Value = -value,
                IssueDate = date,
                Notes = request.Notes?.Trim() ?? line.First().Notes?.Trim(),
            });
        }

        // 3. Dr inventory, Cr WIP.
        if (total > 0)
        {
            await posting.PostAsync(new PostingRequest(wo.LegalEntityId, date, WorkOrderRules.JournalSource, wo.Id, memo, wo.WorkOrderNumber,
            [
                new(await posting.AccountForRoleAsync("inventory", token), total, 0, memo),
                new(await posting.AccountForRoleAsync(SystemRoles.Wip, token), 0, total, memo),
            ]), token);
        }

        // 4. Save.
        await orders.SaveChangesAsync(token);
        return WorkOrderDto.From(wo);
    }, cancellationToken);
}
