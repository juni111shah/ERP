using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Parties;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using ERP.Modules.Purchasing.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Features;

public sealed record PurchaseOrderLineInput(
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate);

public sealed record PurchaseOrderInput(
    Guid LegalEntityId,
    string? Number,
    Guid? RequisitionId,
    Guid? VendorPartyId,
    string VendorName,
    string? VendorContact,
    string? VendorReference,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string Currency,
    string? PaymentTerms,
    string? DeliveryAddress,
    string? Notes,
    List<PurchaseOrderLineInput> Lines,
    JsonObject? CustomFields = null);

public sealed record PurchaseOrderLineDto(
    Guid Id,
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    decimal ReceivedQuantity,
    decimal BilledQuantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate,
    decimal LineTotal,
    decimal ReturnedQuantity = 0);

public sealed record PurchaseOrderSummaryDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid? VendorPartyId,
    string VendorName,
    string? VendorReference,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string Currency,
    PurchaseOrderStatus Status,
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal TotalAmount,
    int LineCount,
    Guid? InvoicedBillId,
    DateTime CreatedAt);

public sealed record PurchaseOrderDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid? RequisitionId,
    Guid? VendorPartyId,
    string VendorName,
    string? VendorContact,
    string? VendorReference,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string Currency,
    PurchaseOrderStatus Status,
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal TotalAmount,
    string? PaymentTerms,
    string? DeliveryAddress,
    string? Notes,
    Guid? InvoicedBillId,
    IReadOnlyList<PurchaseOrderLineDto> Lines,
    JsonObject CustomFields,
    DateTime CreatedAt,
    IReadOnlyList<Guid>? BillIds = null);

/// <summary>How much of an order line to put on this bill.</summary>
public sealed record BillOrderLineInput(Guid LineId, decimal Quantity);

public sealed record GetPurchaseOrder(Guid Id) : IQuery<PurchaseOrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersRead; }
public sealed record CreatePurchaseOrder(PurchaseOrderInput Input) : ICommand<PurchaseOrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
public sealed record UpdatePurchaseOrder(Guid Id, PurchaseOrderInput Input) : ICommand<PurchaseOrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
public sealed record DeletePurchaseOrder(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
public sealed record ChangePurchaseOrderStatus(Guid Id, PurchaseOrderStatus Status) : ICommand<PurchaseOrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
/// <summary>
/// Raises one draft bill for part of the order. With no lines given it takes everything billable: received and not yet billed on a stocked line, ordered and not yet billed on any other line.
/// AllowUnreceived lets a stocked line be billed ahead of its goods: the debit waits on goods-received clearing until they arrive.
/// </summary>
public sealed record ConvertPurchaseOrderToBill(Guid Id, IReadOnlyDictionary<Guid, decimal>? UnitPriceOverrides = null, IReadOnlyList<BillOrderLineInput>? Lines = null, bool AllowUnreceived = false, string? VendorReference = null) : ICommand<BillDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => P.OrdersManage; }

public static class PurchaseOrderBillRules
{
    /// <summary>Override prices for the bill must not be negative and carry at most four decimals.</summary>
    public static bool AreValidOverrides(IReadOnlyDictionary<Guid, decimal>? overrides)
        => overrides is null || overrides.Values.All(p => p >= 0 && MoneyValidation.HasAtMostDecimals(p, 4));
}

public sealed class ConvertPurchaseOrderToBillValidator : AbstractValidator<ConvertPurchaseOrderToBill>
{
    public ConvertPurchaseOrderToBillValidator()
    {
        RuleFor(x => x.UnitPriceOverrides).Must(PurchaseOrderBillRules.AreValidOverrides).WithMessage("Override prices cannot be negative and can have at most 4 decimal places.");
        RuleFor(x => x.VendorReference).MaximumLength(100);
        RuleForEach(x => x.Lines).ChildRules(l => l.RuleFor(x => x.Quantity).GreaterThan(0).MustHaveAtMostDecimals(4));
    }
}

public sealed class PurchaseOrderInputValidator : AbstractValidator<PurchaseOrderInput>
{
    public PurchaseOrderInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.VendorName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(10);
        RuleFor(x => x.OrderDate).MustBePlausibleDate();
        RuleFor(x => x.ExpectedDeliveryDate).MustBePlausibleDate();
        RuleFor(x => x).Must(x => x.ExpectedDeliveryDate is null || x.ExpectedDeliveryDate >= x.OrderDate).WithName("expectedDeliveryDate")
            .WithMessage("The expected delivery date cannot be before the order date.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Purchase order must contain at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
            l.RuleFor(x => x.Quantity).GreaterThan(0).MustHaveAtMostDecimals(4);
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).MustHaveAtMostDecimals(4);
            l.RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100);
            l.RuleFor(x => x.TaxRate).InclusiveBetween(0, 100);
        });
    }
}

public sealed class CreatePurchaseOrderValidator : AbstractValidator<CreatePurchaseOrder>
{
    public CreatePurchaseOrderValidator() => RuleFor(x => x.Input).SetValidator(new PurchaseOrderInputValidator());
}

public sealed class UpdatePurchaseOrderValidator : AbstractValidator<UpdatePurchaseOrder>
{
    public UpdatePurchaseOrderValidator() => RuleFor(x => x.Input).SetValidator(new PurchaseOrderInputValidator());
}

internal sealed class PurchaseOrderHandlers(
    IRepository<PurchaseOrder> orders,
    IRepository<GoodsReceipt> goodsReceipts,
    IRepository<PurchaseOrderLine> lineRows,
    IRepository<PurchaseRequisition> requisitions,
    IReadRepository<Account> accounts,
    IReadRepository<Item> items,
    IReadRepository<Party> parties,
    IReadRepository<TaxCode> taxCodes,
    IRepository<PurchaseOrderBillLine> billLinks,
    IReadRepository<Bill> bills,
    IStockPostingService stock,
    PurchasingApprovalGate approvalGate,
    ISender sender,
    ILegalEntityGuard guard,
    INumberingService numbers,
    IUnitOfWork unitOfWork,
    ICustomFieldService fields,
    IReferenceCheck references) :
    IRequestHandler<GetPurchaseOrder, PurchaseOrderDto>,
    IRequestHandler<CreatePurchaseOrder, PurchaseOrderDto>,
    IRequestHandler<UpdatePurchaseOrder, PurchaseOrderDto>,
    IRequestHandler<DeletePurchaseOrder, Unit>,
    IRequestHandler<ChangePurchaseOrderStatus, PurchaseOrderDto>,
    IRequestHandler<ConvertPurchaseOrderToBill, BillDto>
{
    public async Task<PurchaseOrderDto> Handle(GetPurchaseOrder q, CancellationToken ct)
    {
        var order = await orders.Tracked(new Specification<PurchaseOrder>(x => x.Id == q.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Purchase order '{q.Id}' not found.");
        var billIds = await billLinks.Query(new Specification<PurchaseOrderBillLine>(r => r.OrderId == order.Id)).OrderBy(r => r.CreatedAt).Select(r => r.BillId).Distinct().ToListAsync(ct);
        return await ToDtoAsync(order, fields, ct) with { BillIds = billIds };
    }

    public async Task<PurchaseOrderDto> Handle(CreatePurchaseOrder cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var vendorName = await ValidateHeaderAsync(i, entity.BaseCurrency, ct);
        var order = new PurchaseOrder
        {
            LegalEntityId = entity.Id,
            RequisitionId = i.RequisitionId,
            VendorPartyId = i.VendorPartyId,
            VendorName = vendorName,
            VendorContact = i.VendorContact?.Trim(),
            VendorReference = i.VendorReference?.Trim(),
            OrderDate = i.OrderDate,
            ExpectedDeliveryDate = i.ExpectedDeliveryDate,
            Currency = i.Currency.Trim().ToUpperInvariant(),
            PaymentTerms = i.PaymentTerms,
            DeliveryAddress = i.DeliveryAddress,
            Notes = i.Notes,
            Status = PurchaseOrderStatus.Draft,
        };

        order.Number = string.IsNullOrWhiteSpace(i.Number)
            ? await numbers.NextAsync(Docs.PurchaseOrder, entity.Id, i.OrderDate, ct)
            : i.Number.Trim();

        if (await orders.AnyAsync(x => x.Number == order.Number, ct))
            throw new ConflictException($"Purchase order number '{order.Number}' already exists.");

        order.Lines.AddRange(BuildLinesAndTotals(order, i.Lines));
        await fields.ApplyAsync(Docs.PurchaseOrder, order, i.CustomFields, isCreate: true, ct);

        orders.Add(order);
        await orders.SaveChangesAsync(ct);
        return await ToDtoAsync(order, fields, ct);
    }

    public async Task<PurchaseOrderDto> Handle(UpdatePurchaseOrder cmd, CancellationToken ct)
    {
        var order = await orders.Tracked(new Specification<PurchaseOrder>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Purchase order '{cmd.Id}' not found.");

        if (order.Status == PurchaseOrderStatus.Billed)
            throw new ConflictException("Cannot edit a billed purchase order.");
        if (order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Submitted))
            throw new ConflictException("Only a draft or submitted purchase order can be edited.");

        var i = cmd.Input;
        if (i.LegalEntityId != order.LegalEntityId)
            throw new FieldValidationException("legalEntityId", "The legal entity of a purchase order cannot change.");
        var entity = await guard.RequireAsync(order.LegalEntityId, ct);
        var vendorName = await ValidateHeaderAsync(i, entity.BaseCurrency, ct);
        order.VendorPartyId = i.VendorPartyId;
        order.VendorName = vendorName;
        order.VendorContact = i.VendorContact?.Trim();
        order.VendorReference = i.VendorReference?.Trim();
        order.OrderDate = i.OrderDate;
        order.ExpectedDeliveryDate = i.ExpectedDeliveryDate;
        order.Currency = i.Currency.Trim().ToUpperInvariant();
        order.PaymentTerms = i.PaymentTerms;
        order.DeliveryAddress = i.DeliveryAddress;
        order.Notes = i.Notes;

        // What was approved is no longer what the order says.
        await approvalGate.ReleaseAsync(order.ApprovalRequestId, ct);
        order.ApprovalRequestId = null;

        // Lines are replaced through the line repository: new lines already carry keys, so adding them to the order's collection
        // would make EF treat them as existing rows (and the workspace guard then rejects the write).
        var fresh = BuildLinesAndTotals(order, i.Lines);
        foreach (var old in order.Lines.ToList()) lineRows.Remove(old);
        lineRows.AddRange(fresh);
        await fields.ApplyAsync(Docs.PurchaseOrder, order, i.CustomFields, isCreate: false, ct);

        await orders.SaveChangesAsync(ct);
        order.Lines.Clear(); order.Lines.AddRange(fresh);
        return await ToDtoAsync(order, fields, ct);
    }

    public async Task<Unit> Handle(DeletePurchaseOrder cmd, CancellationToken ct)
    {
        var order = await orders.GetForUpdateAsync(cmd.Id, ct);
        if (order.Status == PurchaseOrderStatus.Billed)
            throw new ConflictException("Cannot delete a billed purchase order.");
        if (order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Cancelled))
            throw new ConflictException("Only a draft or cancelled purchase order can be deleted.");
        if (await goodsReceipts.AnyAsync(x => x.PurchaseOrderId == order.Id, ct))
            throw new ConflictException("This purchase order has goods receipts and cannot be deleted.");

        await ReleaseRequisitionAsync(order, ct);
        await approvalGate.ReleaseAsync(order.ApprovalRequestId, ct);
        orders.Remove(order);
        await orders.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public Task<PurchaseOrderDto> Handle(ChangePurchaseOrderStatus cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(token => ChangeStatusAsync(cmd, token), ct);

    private async Task<PurchaseOrderDto> ChangeStatusAsync(ChangePurchaseOrderStatus cmd, CancellationToken ct)
    {
        var order = await orders.Tracked(new Specification<PurchaseOrder>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Purchase order '{cmd.Id}' not found.");

        PurchasingTransitions.Order.Ensure(order.Status, cmd.Status, isSystem: false);
        if (cmd.Status == PurchaseOrderStatus.Cancelled && order.Lines.Any(l => l.BilledQuantity > 0))
            throw new ConflictException("This purchase order has bills. Void or delete them before cancelling it.");

        var target = cmd.Status;
        if (target is PurchaseOrderStatus.Submitted or PurchaseOrderStatus.Approved)
        {
            var gate = await approvalGate.CheckAsync(Docs.PurchaseOrder, order.Id, $"Purchase order {order.Number} from {order.VendorName}", order.TotalAmount, order.LegalEntityId,
                order.ApprovalRequestId, order.CreatedBy, approving: target == PurchaseOrderStatus.Approved, ct);
            order.ApprovalRequestId = gate.RequestId;
            if (!gate.MayApprove) target = PurchaseOrderStatus.Submitted;
            else if (gate.AutoApproved) target = PurchaseOrderStatus.Approved;
        }

        order.Status = target;
        if (cmd.Status == PurchaseOrderStatus.Cancelled)
        {
            await ReleaseRequisitionAsync(order, ct);
            await approvalGate.ReleaseAsync(order.ApprovalRequestId, ct);
        }
        await orders.SaveChangesAsync(ct);
        return await ToDtoAsync(order, fields, ct);
    }

    /// <summary>The requisition behind a cancelled or deleted order still needs buying, so it goes back to approved and can be ordered again.</summary>
    private async Task ReleaseRequisitionAsync(PurchaseOrder order, CancellationToken ct)
    {
        if (order.RequisitionId is not { } id) return;
        var requisition = await requisitions.Tracked(new Specification<PurchaseRequisition>(r => r.Id == id && r.ConvertedPurchaseOrderId == order.Id)).FirstOrDefaultAsync(ct);
        if (requisition is null) return;
        requisition.Status = PurchaseRequisitionStatus.Approved;
        requisition.ConvertedPurchaseOrderId = null;
    }

    /// <summary>Vendor, currency and name checks shared by create and update; returns the vendor name to store (the party's own name when a party is chosen).</summary>
    private async Task<string> ValidateHeaderAsync(PurchaseOrderInput i, string baseCurrency, CancellationToken ct)
    {
        if (!await references.AllExistAsync<Item>(i.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToList(), ct))
            throw new FieldValidationException("lines", "An item was not found.");
        if (!string.Equals(i.Currency.Trim(), baseCurrency, StringComparison.OrdinalIgnoreCase))
            throw new FieldValidationException("currency", $"The currency must be the legal entity's currency ({baseCurrency}).");
        if (i.VendorPartyId is not { } partyId) return i.VendorName.Trim();
        var party = await parties.FindAsync(partyId, ct) ?? throw new FieldValidationException("vendorPartyId", "Vendor not found.");
        VendorRules.RequireVendor(party);
        return party.Name;
    }

    // One transaction: the bill and the order's billed state are saved together or not at all.
    public Task<BillDto> Handle(ConvertPurchaseOrderToBill cmd, CancellationToken ct)
        => unitOfWork.InTransactionAsync(token => ConvertAsync(cmd, token), ct);

    private async Task<BillDto> ConvertAsync(ConvertPurchaseOrderToBill cmd, CancellationToken ct)
    {
        var order = await orders.Tracked(new Specification<PurchaseOrder>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Purchase order '{cmd.Id}' not found.");

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

        // Find inventory clearing (GR/IR) account
        var clearingAccount = await accounts.Query(new Specification<Account>(a => a.SystemRole == SystemRoles.InventoryClearing && a.IsActive && a.IsPostable))
            .FirstOrDefaultAsync(ct) ?? throw new ConflictException("No account is set up for 'inventory.clearing'. Add one in the chart of accounts.");

        // Accounts are chosen by system role only, never by name or code order.
        async Task<Account> RoleAccountAsync(string role)
            => await accounts.Query(new Specification<Account>(a => a.SystemRole == role && a.IsActive && a.IsPostable)).FirstOrDefaultAsync(ct)
                ?? throw new ConflictException($"No account is set up for '{role}'. Add one in the chart of accounts.");
        var expenseAccount = await RoleAccountAsync(SystemRoles.PurchasesExpense);
        Account? varianceAccount = null;

        var taxByRate = new Dictionary<decimal, (Guid Id, bool Recoverable)>();
        foreach (var rate in order.Lines.Select(l => l.TaxRate).Where(r => r > 0).Distinct())
        {
            var code = await taxCodes.Query(new Specification<TaxCode>(t => t.Rate == rate && t.IsActive && (t.LegalEntityId == null || t.LegalEntityId == order.LegalEntityId)))
                .OrderByDescending(t => t.IsRecoverable).ThenByDescending(t => t.IsDefault).ThenBy(t => t.Code)
                .FirstOrDefaultAsync(ct) ?? throw new ConflictException($"No active tax code with rate {rate:0.####}% exists.");
            taxByRate[rate] = (code.Id, code.IsRecoverable);
        }

        var itemIds = order.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToList();
        var productItemIds = (await items.Query(new Specification<Item>(x => itemIds.Contains(x.Id) && x.Type == ItemType.Product))
            .Select(x => x.Id)
            .ToListAsync(ct)).ToHashSet();

        var inventoryOn = await stock.IsInventoryEnabledAsync(ct);
        var receivedLines = await goodsReceipts.Query(new Specification<GoodsReceipt>(g => g.PurchaseOrderId == order.Id)).SelectMany(g => g.Lines)
            .Select(l => new { l.PurchaseOrderLineId, l.QuantityReceived, l.NoStockMoved }).ToListAsync(ct);

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
        var count = (await billLinks.Query(new Specification<PurchaseOrderBillLine>(r => r.OrderId == order.Id)).Select(r => r.BillId).Distinct().ToListAsync(ct)).Count;
        for (var n = count + 1; ; n++)
        {
            var candidate = n == 1 ? baseReference : $"{baseReference}-{n}";
            if (!await bills.AnyAsync(b => b.PartyId == order.VendorPartyId && b.VendorReference == candidate && b.Status != DocumentStatus.Voided, ct)) return candidate;
        }
    }

    /// <summary>Builds the order's lines from the input and sets its totals; the caller attaches the lines.</summary>
    private static List<PurchaseOrderLine> BuildLinesAndTotals(PurchaseOrder order, List<PurchaseOrderLineInput> lines)
    {
        var built = new List<PurchaseOrderLine>();
        decimal subtotal = 0;
        decimal taxTotal = 0;
        decimal discountTotal = 0;

        foreach (var l in lines)
        {
            var baseAmount = l.Quantity * l.UnitPrice;
            var disc = baseAmount * (l.DiscountPercent / 100m);
            var afterDisc = baseAmount - disc;
            var tax = afterDisc * (l.TaxRate / 100m);
            var lineTotal = afterDisc + tax;

            subtotal += afterDisc;
            discountTotal += disc;
            taxTotal += tax;

            built.Add(new PurchaseOrderLine
            {
                OrderId = order.Id,
                ItemId = l.ItemId,
                ItemCode = l.ItemCode.Trim(),
                Description = l.Description.Trim(),
                Quantity = l.Quantity,
                UnitOfMeasure = string.IsNullOrWhiteSpace(l.UnitOfMeasure) ? "EA" : l.UnitOfMeasure.Trim(),
                UnitPrice = l.UnitPrice,
                DiscountPercent = l.DiscountPercent,
                TaxRate = l.TaxRate,
                LineTotal = Math.Round(lineTotal, 2)
            });
        }

        order.SubTotal = Math.Round(subtotal, 2);
        order.DiscountTotal = Math.Round(discountTotal, 2);
        order.TaxTotal = Math.Round(taxTotal, 2);
        order.TotalAmount = Math.Round(subtotal + taxTotal, 2);
        return built;
    }

    public static async Task<PurchaseOrderDto> ToDtoAsync(PurchaseOrder o, ICustomFieldService fields, CancellationToken ct)
    {
        var cf = await fields.ReadAsync(Docs.PurchaseOrder, o, ct);
        return new PurchaseOrderDto(
            o.Id,
            o.Number,
            o.LegalEntityId,
            o.RequisitionId,
            o.VendorPartyId,
            o.VendorName,
            o.VendorContact,
            o.VendorReference,
            o.OrderDate,
            o.ExpectedDeliveryDate,
            o.Currency,
            o.Status,
            o.SubTotal,
            o.TaxTotal,
            o.DiscountTotal,
            o.TotalAmount,
            o.PaymentTerms,
            o.DeliveryAddress,
            o.Notes,
            o.InvoicedBillId,
            o.Lines.Select(l => new PurchaseOrderLineDto(
                l.Id, l.ItemId, l.ItemCode, l.Description, l.Quantity, l.ReceivedQuantity,
                l.BilledQuantity, l.UnitOfMeasure, l.UnitPrice, l.DiscountPercent, l.TaxRate, l.LineTotal, l.ReturnedQuantity)).ToList(),
            cf,
            o.CreatedAt);
    }
}
