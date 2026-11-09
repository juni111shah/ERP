using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;
using MediatR;

namespace ERP.Modules.Purchasing.Features;

internal sealed class ConvertPurchaseOrderToBillHandler(
    IPurchaseOrderRepository orders,
    IGoodsReceiptRepository goodsReceipts,
    IPurchasingAccountRepository accounts,
    IPurchasingItemRepository items,
    IPurchasingTaxCodeRepository taxCodes,
    IPurchaseOrderBillLineRepository billLinks,
    IReadRepository<Bill> bills,
    IStockPostingService stock,
    ISender sender,
    IUnitOfWork unitOfWork) : ICommandHandler<ConvertPurchaseOrderToBillCommand, BillDto>
{
    // One transaction: the bill and the order's billed state are saved together or not at all.
    public Task<BillDto> Handle(ConvertPurchaseOrderToBillCommand cmd, CancellationToken ct)
        => unitOfWork.InTransactionAsync(token => ConvertAsync(cmd, token), ct);

    private async Task<BillDto> ConvertAsync(ConvertPurchaseOrderToBillCommand cmd, CancellationToken ct)
    {
        // 1. The order, unless everything is billed already.
        var order = await orders.FindWithLinesForUpdateAsync(cmd.Id, ct) ?? throw new NotFoundException($"Purchase order '{cmd.Id}' not found.");

        // Nothing is left to bill, so asking again returns the last bill instead of raising another.
        if (order.Status == PurchaseOrderStatus.Billed && order.InvoicedBillId is { } lastBill)
        {
            return await sender.Send(new GetBillByIdQuery(lastBill), ct);
        }

        PurchasingTransitions.Order.Ensure(order.Status, PurchaseOrderStatus.Billed, isSystem: true);

        if (!order.VendorPartyId.HasValue)
        {
            throw new ConflictException("Purchase order must have an assigned Vendor Party to generate an accounting bill.");
        }

        // 2. The accounts and tax codes the bill posts to.
        // Find inventory clearing (GR/IR) account
        var clearingAccount = await accounts.FindPostableByRoleAsync(SystemRoles.InventoryClearing, ct)
            ?? throw new ConflictException("No account is set up for 'inventory.clearing'. Add one in the chart of accounts.");

        // Accounts are chosen by system role only, never by name or code order.
        async Task<Account> RoleAccountAsync(string role)
            => await accounts.FindPostableByRoleAsync(role, ct)
                ?? throw new ConflictException($"No account is set up for '{role}'. Add one in the chart of accounts.");
        var expenseAccount = await RoleAccountAsync(SystemRoles.PurchasesExpense);
        Account? varianceAccount = null;

        var taxByRate = new Dictionary<decimal, (Guid Id, bool Recoverable)>();
        foreach (var rate in order.Lines.Select(l => l.TaxRate).Where(r => r > 0).Distinct())
        {
            var code = await taxCodes.FindBestForRateAsync(rate, order.LegalEntityId, ct) ?? throw new ConflictException($"No active tax code with rate {rate:0.####}% exists.");
            taxByRate[rate] = (code.Id, code.IsRecoverable);
        }

        // 3. What each line can bill: stocked lines only what was received (unless billing ahead), others what was ordered.
        var itemIds = order.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToList();
        var productItemIds = (await items.ListProductIdsAsync(itemIds, ct)).ToHashSet();

        var inventoryOn = await stock.IsInventoryEnabledAsync(ct);
        var receivedLines = await goodsReceipts.ListReceivedLinesOfOrderAsync(order.Id, ct);

        if (cmd.Lines is { } wanted && wanted.FirstOrDefault(w => order.Lines.All(l => l.Id != w.LineId)) is { } unknown)
            throw new FieldValidationException("lines", $"Line '{unknown.LineId}' is not on purchase order '{order.Number}'.");
        var requested = cmd.Lines?.GroupBy(w => w.LineId).ToDictionary(g => g.Key, g => g.Sum(w => w.Quantity));

        var billLines = new List<BillLineInput>();
        var links = new List<PurchaseOrderBillLine>();
        foreach (var l in order.Lines)
        {
            var isProduct = l.ItemId.HasValue && productItemIds.Contains(l.ItemId.Value);
            var stockExpected = isProduct && inventoryOn;
            var billable = (stockExpected && !cmd.AllowUnreceived ? l.ReceivedQuantity : Math.Max(l.Quantity, l.ReceivedQuantity)) - l.BilledQuantity;
            if (requested is not null && !requested.ContainsKey(l.Id)) continue;
            var billQty = requested?[l.Id] ?? Math.Max(billable, 0m);
            if (billQty > billable)
                throw new ConflictException(stockExpected && !cmd.AllowUnreceived
                    ? $"'{l.Description}': only {Math.Max(billable, 0m):0.####} is received and not yet billed; cannot bill {billQty:0.####}. Receive the goods first, or set allowUnreceived to bill ahead of them."
                    : $"'{l.Description}': only {Math.Max(billable, 0m):0.####} is left to bill; cannot bill {billQty:0.####}.");
            if (billQty <= 0) continue;

            var poNetPrice = Math.Round(l.UnitPrice * (1m - (l.DiscountPercent / 100m)), 4);
            var targetPrice = poNetPrice;
            if (cmd.UnitPriceOverrides != null && cmd.UnitPriceOverrides.TryGetValue(l.Id, out var overridePrice))
            {
                targetPrice = overridePrice;
            }

            var mine = receivedLines.Where(r => r.PurchaseOrderLineId == l.Id).ToList();
            var stockedReceived = isProduct ? Math.Min(mine.Where(r => !r.NoStockMoved).Sum(r => r.QuantityReceived), l.ReceivedQuantity) : 0m;
            var (clearingQty, expenseQty) = Split(l.BilledQuantity, billQty, stockedReceived, l.ReceivedQuantity - stockedReceived, stockExpected);
            (Guid Id, bool Recoverable)? tax = l.TaxRate > 0 ? taxByRate[l.TaxRate] : null;

            if (clearingQty > 0)
            {
                billLines.Add(new BillLineInput(
                    Description: $"{order.Number}: {l.Description}",
                    Quantity: clearingQty,
                    Price: poNetPrice,
                    AccountId: clearingAccount.Id,
                    TaxCodeId: tax?.Id,
                    ItemId: l.ItemId
                ));

                // The vendor's price differs from the price the goods were received at: clearing still clears at the receipt price and the difference, up or down, goes to variance.
                var priceDiff = targetPrice - poNetPrice;
                if (priceDiff != 0)
                {
                    varianceAccount ??= await RoleAccountAsync(SystemRoles.PurchaseVariance);
                    billLines.Add(new BillLineInput(
                        Description: $"{order.Number}: Purchase price variance for {l.Description}",
                        Quantity: clearingQty,
                        Price: priceDiff,
                        AccountId: varianceAccount.Id,
                        TaxCodeId: tax?.Id,
                        ItemId: l.ItemId
                    ));
                }
            }

            if (expenseQty > 0)
            {
                billLines.Add(new BillLineInput(
                    Description: $"{order.Number}: {l.Description}",
                    Quantity: expenseQty,
                    Price: targetPrice,
                    AccountId: expenseAccount.Id,
                    TaxCodeId: tax?.Id,
                    ItemId: l.ItemId
                ));
            }

            links.Add(new PurchaseOrderBillLine
            {
                OrderId = order.Id, OrderLineId = l.Id, Quantity = billQty, ClearingQuantity = clearingQty, UnitPrice = targetPrice, TaxRate = l.TaxRate, TaxRecoverable = tax?.Recoverable ?? true,
            });
        }

        if (links.Count == 0)
            throw new ConflictException("There is nothing to bill on this purchase order: everything received is already billed. Receive the goods first, or set allowUnreceived to bill ahead of them.");

        // 4. Raise the bill.
        var billDate = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        var bill = await sender.Send(new CreateBillCommand(new BillInput(
            LegalEntityId: order.LegalEntityId,
            PartyId: order.VendorPartyId.Value,
            VendorReference: cmd.VendorReference?.Trim() is { Length: > 0 } reference ? reference : await NextBillReferenceAsync(order, ct),
            BillDate: billDate,
            // Vendor payment terms apply unless the expected delivery date is a usable due date (not before the bill date).
            DueDate: order.ExpectedDeliveryDate is { } due && due >= billDate ? due : null,
            Lines: billLines,
            Notes: $"Generated from Purchase Order {order.Number}"
        )), ct);

        // 5. Record what was billed on the order.
        foreach (var link in links)
        {
            link.BillId = bill.Id;
            order.Lines.First(l => l.Id == link.OrderLineId).BilledQuantity += link.Quantity;
        }
        billLinks.AddRange(links);

        order.InvoicedBillId = bill.Id;
        await PurchaseOrderProgress.RefreshAsync(order, items, stock, ct);
        await orders.SaveChangesAsync(ct);

        return bill;
    }

    /// <summary>
    /// Which part of this bill's quantity goes to clearing and which to expense. Units count from the start of the line: first what was received into stock (it credited clearing),
    /// then what was received without moving stock (nothing to clear), then what has not arrived, which waits on clearing only if stock will move when it does.
    /// </summary>
    private static (decimal Clearing, decimal Expense) Split(decimal alreadyBilled, decimal quantity, decimal stockedReceived, decimal unstockedReceived, bool stockExpected)
    {
        decimal Overlap(decimal from, decimal to) => Math.Max(0m, Math.Min(alreadyBilled + quantity, to) - Math.Max(alreadyBilled, from));
        var clearing = Overlap(0m, stockedReceived);
        var expense = Overlap(stockedReceived, stockedReceived + unstockedReceived);
        var ahead = quantity - clearing - expense;
        return stockExpected ? (clearing + ahead, expense) : (clearing, expense + ahead);
    }

    /// <summary>The order's reference for its first bill, then the same with -2, -3...: a vendor reference is unique among the vendor's bills.</summary>
    private async Task<string> NextBillReferenceAsync(PurchaseOrder order, CancellationToken ct)
    {
        var baseReference = order.VendorReference ?? order.Number;
        var count = (await billLinks.ListDistinctBillIdsOfOrderAsync(order.Id, ct)).Count;
        for (var n = count + 1; ; n++)
        {
            var candidate = n == 1 ? baseReference : $"{baseReference}-{n}";
            if (!await bills.AnyAsync(b => b.PartyId == order.VendorPartyId && b.VendorReference == candidate && b.Status != DocumentStatus.Voided, ct)) return candidate;
        }
    }
}
