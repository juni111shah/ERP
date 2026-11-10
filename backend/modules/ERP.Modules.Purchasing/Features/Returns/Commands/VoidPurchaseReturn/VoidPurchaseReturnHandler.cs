using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Catalog;
using ERP.Domain.Platform;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

internal sealed class VoidPurchaseReturnHandler(
    IPurchaseReturnRepository returns,
    IPurchaseOrderRepository orders,
    IPurchaseOrderBillLineRepository billLinks,
    IPurchasingBillRepository bills,
    IJournalEntryRepository journals,
    IReadRepository<Item> items,
    IStockPostingService stock,
    IPostingService posting,
    IRepository<AuditEvent> audit,
    ICurrentUser user,
    IClock clock,
    IUnitOfWork uow) : ICommandHandler<VoidPurchaseReturnCommand, PurchaseReturnDto>
{
    public Task<PurchaseReturnDto> Handle(VoidPurchaseReturnCommand cmd, CancellationToken ct) => uow.InTransactionAsync(token => VoidAsync(cmd, token), ct);

    private async Task<PurchaseReturnDto> VoidAsync(VoidPurchaseReturnCommand cmd, CancellationToken ct)
    {
        // 1. Only a posted return can be voided.
        var ret = await returns.GetWithLinesAndSharesForUpdateAsync(cmd.Id, ct);
        if (ret.Status != PurchaseReturnStatus.Posted) throw new ConflictException("Only a posted return can be voided.");

        // 2. Reverse the journal and the stock.
        if (ret.JournalEntryId is { } journalId)
        {
            var journal = await journals.GetWithLinesForUpdateAsync(journalId, ct);
            await posting.ReverseTodayAsync(journal, VoidMemo.For(ret.Number, cmd.Reason), ct);
        }
        await stock.ReverseIssueAsync(ret.Number, ct); // no journal of its own: the movements carry none and the reversal above covers them

        // 3. Give the bills back what the return took off them.
        foreach (var share in ret.BillShares)
        {
            var bill = await bills.FindForUpdateAsync(share.BillId, ct);
            if (bill is not null)
            {
                bill.Paid -= share.AppliedToBill;
                BillStatus.Refresh(bill);
            }
            if (share.OrderBillLineId is { } linkId && await billLinks.FindForUpdateAsync(linkId, ct) is { } link)
                link.ReturnedQuantity -= share.Quantity;
        }

        // 4. Put the quantities back on the order.
        if (ret.PurchaseOrderId is { } orderId && await orders.FindWithLinesForUpdateAsync(orderId, ct) is { } order)
        {
            foreach (var l in ret.Lines)
            {
                var line = order.Lines.First(x => x.Id == l.OrderLineId);
                line.ReceivedQuantity += l.Quantity;
                line.BilledQuantity += ret.BillShares.Where(s => s.OrderLineId == line.Id).Sum(s => s.Quantity);
                line.ReturnedQuantity -= l.Quantity;
            }
            await PurchaseOrderProgress.RefreshAsync(order, items, stock, ct);
        }

        // 5. Mark it voided and audit the reason.
        ret.Status = PurchaseReturnStatus.Voided;
        VoidMemo.Stamp(ret, cmd.Reason, clock, user);
        VoidMemo.Audit(audit, user, clock, nameof(PurchaseReturn), ret.Id, ret.Number, cmd.Reason!);
        await returns.SaveChangesAsync(ct);
        return PurchaseReturnDto.From(ret);
    }
}
