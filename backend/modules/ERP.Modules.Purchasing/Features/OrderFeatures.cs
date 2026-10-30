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
    decimal LineTotal);

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
    DateTime CreatedAt);

public sealed record GetPurchaseOrder(Guid Id) : IQuery<PurchaseOrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersRead; }
public sealed record CreatePurchaseOrder(PurchaseOrderInput Input) : ICommand<PurchaseOrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
public sealed record UpdatePurchaseOrder(Guid Id, PurchaseOrderInput Input) : ICommand<PurchaseOrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
public sealed record DeletePurchaseOrder(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
public sealed record ChangePurchaseOrderStatus(Guid Id, PurchaseOrderStatus Status) : ICommand<PurchaseOrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
public sealed record ConvertPurchaseOrderToBill(Guid Id, IReadOnlyDictionary<Guid, decimal>? UnitPriceOverrides = null) : ICommand<BillDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => P.OrdersManage; }

public static class PurchaseOrderBillRules
{
    /// <summary>Override prices for the bill must not be negative and carry at most four decimals.</summary>
    public static bool AreValidOverrides(IReadOnlyDictionary<Guid, decimal>? overrides)
        => overrides is null || overrides.Values.All(p => p >= 0 && MoneyValidation.HasAtMostDecimals(p, 4));
}

public sealed class ConvertPurchaseOrderToBillValidator : AbstractValidator<ConvertPurchaseOrderToBill>
{
    public ConvertPurchaseOrderToBillValidator()
        => RuleFor(x => x.UnitPriceOverrides).Must(PurchaseOrderBillRules.AreValidOverrides).WithMessage("Override prices cannot be negative and can have at most 4 decimal places.");
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
    IReadRepository<Account> accounts,
    IReadRepository<Item> items,
    IReadRepository<Party> parties,
    IReadRepository<TaxCode> taxCodes,
    ISender sender,
    ILegalEntityGuard guard,
    INumberingService numbers,
    IUnitOfWork unitOfWork,
    ICustomFieldService fields) :
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
        return await ToDtoAsync(order, fields, ct);
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

        PopulateLinesAndTotals(order, i.Lines);
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

        PopulateLinesAndTotals(order, i.Lines);
        await fields.ApplyAsync(Docs.PurchaseOrder, order, i.CustomFields, isCreate: false, ct);

        await orders.SaveChangesAsync(ct);
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

        orders.Remove(order);
        await orders.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<PurchaseOrderDto> Handle(ChangePurchaseOrderStatus cmd, CancellationToken ct)
    {
        var order = await orders.Tracked(new Specification<PurchaseOrder>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Purchase order '{cmd.Id}' not found.");

        PurchasingTransitions.Order.Ensure(order.Status, cmd.Status, isSystem: false);

        order.Status = cmd.Status;
        await orders.SaveChangesAsync(ct);
        return await ToDtoAsync(order, fields, ct);
    }

    /// <summary>Vendor, currency and name checks shared by create and update; returns the vendor name to store (the party's own name when a party is chosen).</summary>
    private async Task<string> ValidateHeaderAsync(PurchaseOrderInput i, string baseCurrency, CancellationToken ct)
    {
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

        if (order.InvoicedBillId.HasValue)
        {
            return await sender.Send(new GetBill(order.InvoicedBillId.Value), ct);
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

        var taxByRate = new Dictionary<decimal, Guid>();
        foreach (var rate in order.Lines.Select(l => l.TaxRate).Where(r => r > 0).Distinct())
        {
            var code = await taxCodes.Query(new Specification<TaxCode>(t => t.Rate == rate && t.IsActive && (t.LegalEntityId == null || t.LegalEntityId == order.LegalEntityId)))
                .OrderByDescending(t => t.IsRecoverable).ThenByDescending(t => t.IsDefault).ThenBy(t => t.Code)
                .FirstOrDefaultAsync(ct) ?? throw new ConflictException($"No active tax code with rate {rate:0.####}% exists.");
            taxByRate[rate] = code.Id;
        }

        var itemIds = order.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToList();
        var productItemIds = (await items.Query(new Specification<Item>(x => itemIds.Contains(x.Id) && x.Type == ItemType.Product))
            .Select(x => x.Id)
            .ToListAsync(ct)).ToHashSet();

        var billLines = new List<BillLineInput>();
        foreach (var l in order.Lines)
        {
            var isProduct = l.ItemId.HasValue && productItemIds.Contains(l.ItemId.Value);
            var poNetPrice = Math.Round(l.UnitPrice * (1m - (l.DiscountPercent / 100m)), 4);
            var targetPrice = poNetPrice;
            if (cmd.UnitPriceOverrides != null && cmd.UnitPriceOverrides.TryGetValue(l.Id, out var overridePrice))
            {
                targetPrice = overridePrice;
            }

            var billQty = Math.Max(l.Quantity, l.ReceivedQuantity);
            var receivedQty = isProduct ? Math.Min(billQty, l.ReceivedQuantity) : 0m;
            var unreceivedQty = billQty - receivedQty;
            Guid? taxCodeId = l.TaxRate > 0 ? taxByRate[l.TaxRate] : null;

            if (receivedQty > 0)
            {
                billLines.Add(new BillLineInput(
                    Description: $"{order.Number}: {l.Description}",
                    Quantity: receivedQty,
                    Price: poNetPrice,
                    AccountId: clearingAccount.Id,
                    TaxCodeId: taxCodeId,
                    ItemId: l.ItemId
                ));

                var priceDiff = targetPrice - poNetPrice;
                if (priceDiff != 0)
                {
                    varianceAccount ??= await RoleAccountAsync(SystemRoles.PurchaseVariance);
                    billLines.Add(new BillLineInput(
                        Description: $"{order.Number}: Purchase price variance for {l.Description}",
                        Quantity: receivedQty,
                        Price: priceDiff,
                        AccountId: varianceAccount.Id,
                        TaxCodeId: taxCodeId,
                        ItemId: l.ItemId
                    ));
                }
            }

            if (unreceivedQty > 0)
            {
                billLines.Add(new BillLineInput(
                    Description: $"{order.Number}: {l.Description}" + (receivedQty > 0 ? " (Pending)" : ""),
                    Quantity: unreceivedQty,
                    Price: targetPrice,
                    AccountId: expenseAccount.Id,
                    TaxCodeId: taxCodeId,
                    ItemId: l.ItemId
                ));
            }
        }

        var billDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var bill = await sender.Send(new CreateBill(new BillInput(
            LegalEntityId: order.LegalEntityId,
            PartyId: order.VendorPartyId.Value,
            VendorReference: order.VendorReference ?? order.Number,
            BillDate: billDate,
            // Vendor payment terms apply unless the expected delivery date is a usable due date (not before the bill date).
            DueDate: order.ExpectedDeliveryDate is { } due && due >= billDate ? due : null,
            Lines: billLines,
            Notes: $"Generated from Purchase Order {order.Number}"
        )), ct);

        foreach (var l in order.Lines)
        {
            l.BilledQuantity = Math.Max(l.Quantity, l.ReceivedQuantity);
        }

        order.Status = PurchaseOrderStatus.Billed;
        order.InvoicedBillId = bill.Id;
        await orders.SaveChangesAsync(ct);

        return bill;
    }

    private static void PopulateLinesAndTotals(PurchaseOrder order, List<PurchaseOrderLineInput> lines)
    {
        order.Lines.Clear();
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

            order.Lines.Add(new PurchaseOrderLine
            {
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
                l.BilledQuantity, l.UnitOfMeasure, l.UnitPrice, l.DiscountPercent, l.TaxRate, l.LineTotal)).ToList(),
            cf,
            o.CreatedAt);
    }
}
