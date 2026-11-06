using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Domain.Platform;
using ERP.Modules.Purchasing.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Features;

/// <summary>OrderLineId for a return against a purchase order (or a bill raised from one); BillLineId for a debit note on a bill with no order behind it.</summary>
public sealed record PurchaseReturnLineInput(Guid? OrderLineId, Guid? BillLineId, decimal Quantity);
/// <summary>At least one of PurchaseOrderId or BillId. WarehouseId defaults to where the order's goods were received.</summary>
public sealed record PurchaseReturnInput(Guid? PurchaseOrderId, Guid? BillId, DateOnly ReturnDate, string Reason, List<PurchaseReturnLineInput> Lines, Guid? WarehouseId = null, string? Notes = null);

public sealed record PurchaseReturnLineDto(Guid Id, Guid? OrderLineId, Guid? BillLineId, Guid? ItemId, string ItemCode, string Description, decimal Quantity);
public sealed record PurchaseReturnBillShareDto(Guid BillId, decimal Quantity, decimal Amount, decimal AppliedToBill);
public sealed record PurchaseReturnSummaryDto(
    Guid Id, string Number, Guid LegalEntityId, Guid? PurchaseOrderId, string? PurchaseOrderNumber, Guid? BillId, Guid VendorPartyId, string VendorName,
    DateOnly ReturnDate, PurchaseReturnStatus Status, decimal TotalAmount, decimal StockValue, int LineCount, DateTime CreatedAt);
public sealed record PurchaseReturnDto(
    Guid Id, string Number, Guid LegalEntityId, Guid? PurchaseOrderId, string? PurchaseOrderNumber, Guid? BillId, Guid VendorPartyId, string VendorName,
    DateOnly ReturnDate, PurchaseReturnStatus Status, string Reason, string? Notes, Guid? WarehouseId, decimal SubTotal, decimal TaxTotal, decimal TotalAmount, decimal StockValue, Guid? JournalEntryId,
    IReadOnlyList<PurchaseReturnLineDto> Lines, IReadOnlyList<PurchaseReturnBillShareDto> BillShares, string? VoidReason, DateTime? VoidedAt, DateTime CreatedAt);

public sealed record GetPurchaseReturn(Guid Id) : IQuery<PurchaseReturnDto>, IRequirePermission { public string RequiredPermission => P.ReturnsRead; }
public sealed record CreatePurchaseReturn(PurchaseReturnInput Input) : ICommand<PurchaseReturnDto>, IRequirePermission { public string RequiredPermission => P.ReturnsManage; }
public sealed record DeletePurchaseReturn(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.ReturnsManage; }
/// <summary>
/// Goods not billed yet: Dr goods-received clearing, Cr inventory, and the order simply has less received. Goods already billed: Dr payables (with the vendor), Cr inventory and recoverable input tax,
/// and the bill's balance goes down. A gap between stock cost and the amount cleared goes to purchase variance.
/// </summary>
public sealed record PostPurchaseReturn(Guid Id) : ICommand<PurchaseReturnDto>, IRequirePermission { public string RequiredPermission => P.ReturnsManage; }
/// <summary>Reverses the journal, puts the goods back at the cost they left at and restores the order and bill balances.</summary>
public sealed record VoidPurchaseReturn(Guid Id, string? Reason) : ICommand<PurchaseReturnDto>, IRequirePermission { public string RequiredPermission => P.ReturnsManage; }

public sealed class CreatePurchaseReturnValidator : AbstractValidator<CreatePurchaseReturn>
{
    public CreatePurchaseReturnValidator()
    {
        RuleFor(x => x.Input).Must(i => i.PurchaseOrderId is not null || i.BillId is not null).WithName("purchaseOrderId").WithMessage("Raise the return against a purchase order or a bill.");
        RuleFor(x => x.Input.ReturnDate).MustBePlausibleDate();
        RuleFor(x => x.Input.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Input.Notes).MaximumLength(1000);
        RuleFor(x => x.Input.Lines).NotEmpty().WithMessage("A return needs at least one line.");
        RuleFor(x => x.Input.Lines).Must(l => l is null || l.Select(x => (x.OrderLineId, x.BillLineId)).Distinct().Count() == l.Count).WithMessage("A line can appear only once.");
        RuleForEach(x => x.Input.Lines).ChildRules(l => l.RuleFor(x => x.Quantity).GreaterThan(0).MustHaveAtMostDecimals(4));
    }
}

public sealed class VoidPurchaseReturnValidator : AbstractValidator<VoidPurchaseReturn>
{
    public VoidPurchaseReturnValidator() => RuleFor(x => x.Reason).ValidVoidReason();
}

internal sealed class PurchaseReturnHandlers(
    IRepository<PurchaseReturn> returns,
    IRepository<PurchaseOrder> orders,
    IRepository<PurchaseOrderBillLine> billLinks,
    IRepository<Bill> bills,
    IRepository<JournalEntry> journals,
    IReadRepository<GoodsReceipt> receipts,
    IReadRepository<Item> items,
    IStockPostingService stock,
    IPostingService posting,
    ILegalEntityGuard guard,
    INumberingService numbers,
    IRepository<AuditEvent> audit,
    ICurrentUser user,
    IClock clock,
    IUnitOfWork uow) :
    IRequestHandler<GetPurchaseReturn, PurchaseReturnDto>,
    IRequestHandler<CreatePurchaseReturn, PurchaseReturnDto>,
    IRequestHandler<DeletePurchaseReturn, Unit>,
    IRequestHandler<PostPurchaseReturn, PurchaseReturnDto>,
    IRequestHandler<VoidPurchaseReturn, PurchaseReturnDto>
{
    public async Task<PurchaseReturnDto> Handle(GetPurchaseReturn q, CancellationToken ct) => ToDto(await LoadAsync(q.Id, ct));

    public async Task<PurchaseReturnDto> Handle(CreatePurchaseReturn cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var (order, bill) = await SourceAsync(i.PurchaseOrderId, i.BillId, ct);
        var legalEntityId = order?.LegalEntityId ?? bill!.LegalEntityId;
        await guard.RequireAsync(legalEntityId, ct);
        var vendor = order?.VendorPartyId ?? bill?.PartyId ?? throw new ConflictException("This purchase order has no vendor party.");
        if (i.WarehouseId is { } warehouseId && !await stock.IsWarehouseUsableAsync(warehouseId, legalEntityId, ct))
            throw new FieldValidationException("warehouseId", "The warehouse was not found, is not active, or belongs to another legal entity.");

        var ret = new PurchaseReturn
        {
            LegalEntityId = legalEntityId,
            PurchaseOrderId = order?.Id,
            PurchaseOrderNumber = order?.Number,
            BillId = bill?.Id,
            VendorPartyId = vendor,
            VendorName = order?.VendorName ?? bill!.PartyName ?? string.Empty,
            ReturnDate = i.ReturnDate,
            Reason = i.Reason.Trim(),
            Notes = i.Notes,
            WarehouseId = i.WarehouseId,
            Number = await numbers.NextAsync(Docs.PurchaseReturn, legalEntityId, i.ReturnDate, ct),
        };
        foreach (var l in i.Lines)
        {
            if (order is not null)
            {
                var line = order.Lines.FirstOrDefault(x => x.Id == l.OrderLineId) ?? throw new FieldValidationException("lines", "A line does not belong to this purchase order.");
                EnsureWithinReceived(line, l.Quantity);
                ret.Lines.Add(new PurchaseReturnLine { OrderLineId = line.Id, ItemId = line.ItemId, ItemCode = line.ItemCode, Description = line.Description, Quantity = l.Quantity });
            }
            else
            {
                var line = bill!.Lines.FirstOrDefault(x => x.Id == l.BillLineId) ?? throw new FieldValidationException("lines", "A line does not belong to this bill.");
                ret.Lines.Add(new PurchaseReturnLine { BillLineId = line.Id, ItemId = line.ItemId, Description = line.Description, Quantity = l.Quantity });
            }
        }

        returns.Add(ret);
        await returns.SaveChangesAsync(ct);
        return ToDto(ret);
    }

    public async Task<Unit> Handle(DeletePurchaseReturn cmd, CancellationToken ct)
    {
        var ret = await returns.GetForUpdateAsync(cmd.Id, ct);
        if (ret.Status != PurchaseReturnStatus.Draft) throw new ConflictException("Only a draft return can be deleted. Void it instead.");
        returns.Remove(ret);
        await returns.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public Task<PurchaseReturnDto> Handle(PostPurchaseReturn cmd, CancellationToken ct) => uow.InTransactionAsync(token => PostAsync(cmd, token), ct);

    /// <summary>What the return adds up to for its journal.</summary>
    private sealed class Totals
    {
        public decimal Payable, InputTax, Clearing, StockValue, ExpenseCredit, Net, Tax;
        public readonly Dictionary<Guid, decimal> AccountCredits = new();
    }

    private async Task<PurchaseReturnDto> PostAsync(PostPurchaseReturn cmd, CancellationToken ct)
    {
        var ret = await LoadAsync(cmd.Id, ct);
        if (ret.Status != PurchaseReturnStatus.Draft) throw new ConflictException("Only a draft return can be posted.");
        var (order, bill) = await SourceAsync(ret.PurchaseOrderId, ret.BillId, ct);
        var totals = new Totals();

        if (order is not null) await PostAgainstOrderAsync(ret, order, totals, ct);
        else await PostAgainstBillAsync(ret, bill!, totals, ct);

        ret.SubTotal = totals.Net; ret.TaxTotal = totals.Tax; ret.TotalAmount = totals.Payable; ret.StockValue = totals.StockValue;
        var journal = await PostJournalAsync(ret, totals, ct);
        ret.JournalEntryId = journal?.Id;
        ret.Status = PurchaseReturnStatus.Posted;
        await returns.SaveChangesAsync(ct);
        return ToDto(ret);
    }

    private async Task PostAgainstOrderAsync(PurchaseReturn ret, PurchaseOrder order, Totals totals, CancellationToken ct)
    {
        var warehouse = ret.WarehouseId ?? await receipts.Query(new Specification<GoodsReceipt>(g => g.PurchaseOrderId == order.Id && g.WarehouseId != null))
            .OrderByDescending(g => g.ReceiptDate).Select(g => g.WarehouseId).FirstOrDefaultAsync(ct);

        foreach (var l in ret.Lines)
        {
            var line = order.Lines.First(x => x.Id == l.OrderLineId);
            EnsureWithinReceived(line, l.Quantity);

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
        var links = await billLinks.Tracked(new Specification<PurchaseOrderBillLine>(r => r.OrderLineId == line.Id && (ret.BillId == null || r.BillId == ret.BillId)))
            .OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        var left = quantity;
        foreach (var link in links)
        {
            var take = Math.Min(left, link.Quantity - link.ReturnedQuantity);
            if (take <= 0) continue;
            var bill = await bills.Tracked(new Specification<Bill>(b => b.Id == link.BillId)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
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
        var earlier = await returns.Query(new Specification<PurchaseReturn>(r => r.BillId == bill.Id && r.Status == PurchaseReturnStatus.Posted && r.Id != ret.Id))
            .SelectMany(r => r.Lines).Select(l => new { l.BillLineId, l.Quantity }).ToListAsync(ct);
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

    public Task<PurchaseReturnDto> Handle(VoidPurchaseReturn cmd, CancellationToken ct) => uow.InTransactionAsync(token => VoidAsync(cmd, token), ct);

    private async Task<PurchaseReturnDto> VoidAsync(VoidPurchaseReturn cmd, CancellationToken ct)
    {
        var ret = await LoadAsync(cmd.Id, ct);
        if (ret.Status != PurchaseReturnStatus.Posted) throw new ConflictException("Only a posted return can be voided.");

        if (ret.JournalEntryId is { } journalId)
        {
            var journal = await journals.Tracked(new Specification<JournalEntry>(j => j.Id == journalId).Include(j => j.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
            await posting.ReverseTodayAsync(journal, VoidMemo.For(ret.Number, cmd.Reason), ct);
        }
        await stock.ReverseIssueAsync(ret.Number, ct); // no journal of its own: the movements carry none and the reversal above covers them

        foreach (var share in ret.BillShares)
        {
            var bill = await bills.Tracked(new Specification<Bill>(b => b.Id == share.BillId)).FirstOrDefaultAsync(ct);
            if (bill is not null)
            {
                bill.Paid -= share.AppliedToBill;
                BillStatus.Refresh(bill);
            }
            if (share.OrderBillLineId is { } linkId && await billLinks.Tracked(new Specification<PurchaseOrderBillLine>(r => r.Id == linkId)).FirstOrDefaultAsync(ct) is { } link)
                link.ReturnedQuantity -= share.Quantity;
        }

        if (ret.PurchaseOrderId is { } orderId && await orders.Tracked(new Specification<PurchaseOrder>(o => o.Id == orderId)).Include(o => o.Lines).FirstOrDefaultAsync(ct) is { } order)
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

        ret.Status = PurchaseReturnStatus.Voided;
        VoidMemo.Stamp(ret, cmd.Reason, clock, user);
        VoidMemo.Audit(audit, user, clock, nameof(PurchaseReturn), ret.Id, ret.Number, cmd.Reason!);
        await returns.SaveChangesAsync(ct);
        return ToDto(ret);
    }

    /// <summary>The order and bill behind a return: a bill raised from an order brings the order with it.</summary>
    private async Task<(PurchaseOrder? Order, Bill? Bill)> SourceAsync(Guid? orderId, Guid? billId, CancellationToken ct)
    {
        var bill = billId is { } bid
            ? await bills.Tracked(new Specification<Bill>(b => b.Id == bid).Include(b => b.Lines)).FirstOrDefaultAsync(ct) ?? throw new FieldValidationException("billId", "Bill not found.")
            : null;
        if (bill is not null && bill.Status is not (DocumentStatus.Issued or DocumentStatus.PartlyPaid or DocumentStatus.Paid))
            throw new ConflictException($"Bill {bill.Number} is not posted. Post or delete it before recording a return.");
        orderId ??= bill is null ? null : await billLinks.Query(new Specification<PurchaseOrderBillLine>(r => r.BillId == bill.Id)).Select(r => (Guid?)r.OrderId).FirstOrDefaultAsync(ct);
        var order = orderId is { } oid
            ? await orders.Tracked(new Specification<PurchaseOrder>(o => o.Id == oid)).Include(o => o.Lines).FirstOrDefaultAsync(ct) ?? throw new FieldValidationException("purchaseOrderId", "Purchase order not found.")
            : null;
        return (order, bill);
    }

    private static void EnsureWithinReceived(PurchaseOrderLine line, decimal quantity)
    {
        if (quantity > line.ReceivedQuantity)
            throw new FieldValidationException("lines", $"Only {line.ReceivedQuantity:0.####} of '{line.Description}' has been received; cannot return {quantity:0.####}.");
    }

    private async Task<PurchaseReturn> LoadAsync(Guid id, CancellationToken ct)
        => await returns.Tracked(new Specification<PurchaseReturn>(r => r.Id == id)).Include(r => r.Lines).Include(r => r.BillShares).FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Purchase return '{id}' not found.");

    private static PurchaseReturnDto ToDto(PurchaseReturn r) => new(
        r.Id, r.Number, r.LegalEntityId, r.PurchaseOrderId, r.PurchaseOrderNumber, r.BillId, r.VendorPartyId, r.VendorName, r.ReturnDate, r.Status, r.Reason, r.Notes, r.WarehouseId,
        r.SubTotal, r.TaxTotal, r.TotalAmount, r.StockValue, r.JournalEntryId,
        r.Lines.Select(l => new PurchaseReturnLineDto(l.Id, l.OrderLineId, l.BillLineId, l.ItemId, l.ItemCode, l.Description, l.Quantity)).ToList(),
        r.BillShares.Select(s => new PurchaseReturnBillShareDto(s.BillId, s.Quantity, s.Amount, s.AppliedToBill)).ToList(),
        r.VoidReason, r.VoidedAt, r.CreatedAt);
}
