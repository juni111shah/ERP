using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

internal sealed class PostPurchaseReturnHandler(
    IPurchaseReturnRepository returns,
    PurchaseReturnSources sources,
    IPurchaseOrderBillLineRepository billLinks,
    IRepository<Bill> bills,
    IGoodsReceiptRepository receipts,
    IReadRepository<Item> items,
    IStockPostingService stock,
    IPostingService posting,
    IUnitOfWork uow) : ICommandHandler<PostPurchaseReturnCommand, PurchaseReturnDto>
{
    public Task<PurchaseReturnDto> Handle(PostPurchaseReturnCommand cmd, CancellationToken ct) => uow.InTransactionAsync(token => PostAsync(cmd, token), ct);

    /// <summary>What the return adds up to for its journal.</summary>
    private sealed class Totals
    {
        public decimal Payable, InputTax, Clearing, StockValue, ExpenseCredit, Net, Tax;
        public readonly Dictionary<Guid, decimal> AccountCredits = new();
    }

    private async Task<PurchaseReturnDto> PostAsync(PostPurchaseReturnCommand cmd, CancellationToken ct)
    {
        // 1. Only a draft return can be posted.
        var ret = await returns.GetWithLinesAndSharesForUpdateAsync(cmd.Id, ct);
        if (ret.Status != PurchaseReturnStatus.Draft) throw new ConflictException("Only a draft return can be posted.");
        var (order, bill) = await sources.SourceAsync(ret.PurchaseOrderId, ret.BillId, ct);
        var totals = new Totals();

        // 2. Take the goods back against the order (stock and bills) or against the bill alone.
        if (order is not null) await PostAgainstOrderAsync(ret, order, totals, ct);
        else await PostAgainstBillAsync(ret, bill!, totals, ct);

        // 3. Post the journal and mark the return posted.
        ret.SubTotal = totals.Net; ret.TaxTotal = totals.Tax; ret.TotalAmount = totals.Payable; ret.StockValue = totals.StockValue;
        var journal = await PostJournalAsync(ret, totals, ct);
        ret.JournalEntryId = journal?.Id;
        ret.Status = PurchaseReturnStatus.Posted;
        await returns.SaveChangesAsync(ct);
        return PurchaseReturnDto.From(ret);
    }

    private async Task PostAgainstOrderAsync(PurchaseReturn ret, PurchaseOrder order, Totals totals, CancellationToken ct)
    {
        var warehouse = ret.WarehouseId ?? await receipts.LatestWarehouseOfOrderAsync(order.Id, ct);

        foreach (var l in ret.Lines)
        {
            var line = order.Lines.First(x => x.Id == l.OrderLineId);
            PurchaseReturnSources.EnsureWithinReceived(line, l.Quantity);

            var moved = line.ItemId is { } itemId
                ? await stock.IssueStockAsync(new StockIssueRequest(ret.LegalEntityId, itemId, l.Quantity, ret.ReturnDate, ret.Number, $"Return to vendor {ret.Number} (PO {order.Number})", warehouse, SkipJournal: true, EnforceAvailable: true), ct)
                : null;
            if (moved is { } movementId) totals.StockValue += await stock.GetMovementValueAsync(movementId, ct);

            // Goods not billed yet go first: they only have to come off clearing. Against one named bill, the whole quantity comes off that bill.
            var unbilled = ret.BillId is null ? Math.Min(l.Quantity, Math.Max(0m, line.ReceivedQuantity - line.BilledQuantity)) : 0m;
            if (moved is not null) totals.Clearing += Money.Round(unbilled * Math.Round(line.UnitPrice * (1m - (line.DiscountPercent / 100m)), 4));

            var billed = l.Quantity - unbilled;
            if (billed > 0) await TakeFromBillsAsync(ret, line, billed, moved is not null, totals, ct);

            line.ReceivedQuantity -= l.Quantity;
            line.BilledQuantity = Math.Max(0m, line.BilledQuantity - billed);
            line.ReturnedQuantity += l.Quantity;
        }
        await PurchaseOrderProgress.RefreshAsync(order, items, stock, ct);
    }

    private async Task TakeFromBillsAsync(PurchaseReturn ret, PurchaseOrderLine line, decimal quantity, bool stocked, Totals totals, CancellationToken ct)
    {
        var links = await billLinks.ListForOrderLineForUpdateAsync(line.Id, ret.BillId, ct);
        var left = quantity;
        foreach (var link in links)
        {
            var take = Math.Min(left, link.Quantity - link.ReturnedQuantity);
            if (take <= 0) continue;
            var bill = await bills.GetForUpdateAsync(link.BillId, ct);
            if (bill.Status is not (DocumentStatus.Issued or DocumentStatus.PartlyPaid or DocumentStatus.Paid))
                throw new ConflictException($"Bill {bill.Number} is not posted. Post or delete it before recording a return.");

            var net = Money.Round(take * link.UnitPrice);
            var tax = Money.Round(net * link.TaxRate / 100m);
            var amount = net + tax;
            totals.Net += net; totals.Tax += tax; totals.Payable += amount;
            if (link.TaxRecoverable) totals.InputTax += tax;
            if (!stocked) totals.ExpenseCredit += link.TaxRecoverable ? net : amount;

            var applied = Math.Min(amount, bill.Total - bill.Paid);
            bill.Paid += applied;
            BillStatus.Refresh(bill);
            link.ReturnedQuantity += take;
            ret.BillShares.Add(new PurchaseReturnBillShare { ReturnId = ret.Id, BillId = bill.Id, OrderLineId = line.Id, OrderBillLineId = link.Id, Quantity = take, Amount = amount, AppliedToBill = applied });

            left -= take;
            if (left <= 0) break;
        }
        if (left > 0) throw new ConflictException($"'{line.Description}': {left:0.####} of this return has no bill left to come off.");
    }

    /// <summary>A debit note on a bill with no order behind it: no stock moved when it was billed, so none moves now, and the credit goes back to the accounts the bill debited.</summary>
    private async Task PostAgainstBillAsync(PurchaseReturn ret, Bill bill, Totals totals, CancellationToken ct)
    {
        var earlier = await returns.ListPostedLinesOfBillAsync(bill.Id, ret.Id, ct);
        foreach (var l in ret.Lines)
        {
            var line = bill.Lines.First(x => x.Id == l.BillLineId);
            var left = line.Quantity - earlier.Where(e => e.BillLineId == line.Id).Sum(e => e.Quantity);
            if (l.Quantity > left) throw new FieldValidationException("lines", $"Only {left:0.####} of '{line.Description}' is left to return; cannot return {l.Quantity:0.####}.");
            if (line.Price < 0) throw new FieldValidationException("lines", "A variance line cannot be returned.");

            var net = Money.Round(l.Quantity * line.Price);
            var tax = Money.Round(line.TaxAmount * l.Quantity / line.Quantity);
            var amount = net + tax;
            totals.Net += net; totals.Tax += tax; totals.Payable += amount;
            if (line.TaxRecoverable) totals.InputTax += tax;
            var account = line.AccountId!.Value;
            totals.AccountCredits[account] = totals.AccountCredits.GetValueOrDefault(account) + (line.TaxRecoverable ? net : amount);

            var applied = Math.Min(amount, bill.Total - bill.Paid);
            bill.Paid += applied;
            ret.BillShares.Add(new PurchaseReturnBillShare { ReturnId = ret.Id, BillId = bill.Id, Quantity = l.Quantity, Amount = amount, AppliedToBill = applied });
        }
        BillStatus.Refresh(bill);
    }

    private async Task<JournalEntry?> PostJournalAsync(PurchaseReturn ret, Totals t, CancellationToken ct)
    {
        var memo = $"Purchase return {ret.Number}";
        var lines = new List<PostingLine>();
        if (t.Payable > 0) lines.Add(new PostingLine(await posting.AccountForRoleAsync(SystemRoles.ApControl, ct), t.Payable, 0, memo, ret.VendorPartyId));
        if (t.Clearing > 0) lines.Add(new PostingLine(await posting.AccountForRoleAsync(SystemRoles.InventoryClearing, ct), t.Clearing, 0, memo));
        if (t.InputTax > 0) lines.Add(new PostingLine(await posting.AccountForRoleAsync(SystemRoles.TaxInput, ct), 0, t.InputTax, memo));
        if (t.StockValue > 0) lines.Add(new PostingLine(await posting.AccountForRoleAsync("inventory", ct), 0, t.StockValue, memo));
        if (t.ExpenseCredit > 0) lines.Add(new PostingLine(await posting.AccountForRoleAsync(SystemRoles.PurchasesExpense, ct), 0, t.ExpenseCredit, memo));
        lines.AddRange(t.AccountCredits.Where(c => c.Value > 0).Select(c => new PostingLine(c.Key, 0, c.Value, memo)));

        // What the goods cost in stock and what clears against the vendor can differ (the average moved, or the vendor charged another price).
        var gap = lines.Sum(l => l.Debit) - lines.Sum(l => l.Credit);
        if (gap != 0)
        {
            var variance = await posting.AccountForRoleAsync(SystemRoles.PurchaseVariance, ct);
            lines.Add(gap > 0 ? new PostingLine(variance, 0, gap, memo) : new PostingLine(variance, -gap, 0, memo));
        }
        if (lines.Count == 0) return null;
        return await posting.PostAsync(new PostingRequest(ret.LegalEntityId, ret.ReturnDate, "purchase-return", ret.Id, memo, ret.Number, lines), ct);
    }
}
