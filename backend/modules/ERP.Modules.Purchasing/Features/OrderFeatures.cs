using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Finance;
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
public sealed record ConvertPurchaseOrderToBill(Guid Id) : ICommand<BillDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }

public sealed class PurchaseOrderInputValidator : AbstractValidator<PurchaseOrderInput>
{
    public PurchaseOrderInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.VendorName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Purchase order must contain at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
            l.RuleFor(x => x.Quantity).GreaterThan(0);
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
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
    IReadRepository<Account> accounts,
    ISender sender,
    ILegalEntityGuard guard,
    INumberingService numbers,
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
        var order = new PurchaseOrder
        {
            LegalEntityId = entity.Id,
            RequisitionId = i.RequisitionId,
            VendorPartyId = i.VendorPartyId,
            VendorName = i.VendorName.Trim(),
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

        var i = cmd.Input;
        order.VendorPartyId = i.VendorPartyId;
        order.VendorName = i.VendorName.Trim();
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

        orders.Remove(order);
        await orders.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<PurchaseOrderDto> Handle(ChangePurchaseOrderStatus cmd, CancellationToken ct)
    {
        var order = await orders.Tracked(new Specification<PurchaseOrder>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Purchase order '{cmd.Id}' not found.");

        if (order.Status == PurchaseOrderStatus.Billed)
            throw new ConflictException("Cannot change status of an already billed purchase order.");
        if (order.Status == PurchaseOrderStatus.Cancelled)
            throw new ConflictException("Cannot change status of a cancelled purchase order.");
        if (cmd.Status == PurchaseOrderStatus.Billed && !order.InvoicedBillId.HasValue)
            throw new ConflictException("Cannot transition to Billed without converting to a vendor bill.");

        order.Status = cmd.Status;
        await orders.SaveChangesAsync(ct);
        return await ToDtoAsync(order, fields, ct);
    }

    public async Task<BillDto> Handle(ConvertPurchaseOrderToBill cmd, CancellationToken ct)
    {
        var order = await orders.Tracked(new Specification<PurchaseOrder>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Purchase order '{cmd.Id}' not found.");

        if (order.InvoicedBillId.HasValue)
        {
            return await sender.Send(new GetBill(order.InvoicedBillId.Value), ct);
        }

        if (!order.VendorPartyId.HasValue)
        {
            throw new ConflictException("Purchase order must have an assigned Vendor Party to generate an accounting bill.");
        }

        // Find inventory clearing (GR/IR) account or default expense account
        var clearingAccount = await accounts.Query(new Specification<Account>(a => a.SystemRole == SystemRoles.InventoryClearing && a.IsActive && a.IsPostable))
            .FirstOrDefaultAsync(ct);

        var expenseAccount = await accounts.Query(new Specification<Account>(a => a.Type == AccountType.Expense && a.IsPostable))
            .OrderBy(a => a.Code)
            .FirstOrDefaultAsync(ct) ?? throw new ConflictException("No postable expense account configured in Chart of Accounts.");

        var billLines = order.Lines.Select(l =>
        {
            var acctId = (l.ReceivedQuantity > 0 && clearingAccount != null)
                ? clearingAccount.Id
                : expenseAccount.Id;

            return new BillLineInput(
                Description: $"{order.Number}: {l.Description}",
                Quantity: l.Quantity,
                Price: Math.Round(l.UnitPrice * (1m - (l.DiscountPercent / 100m)), 4),
                AccountId: acctId,
                ItemId: l.ItemId
            );
        }).ToList();

        var bill = await sender.Send(new CreateBill(new BillInput(
            LegalEntityId: order.LegalEntityId,
            PartyId: order.VendorPartyId.Value,
            VendorReference: order.VendorReference ?? order.Number,
            BillDate: DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate: order.ExpectedDeliveryDate,
            Lines: billLines,
            Notes: $"Generated from Purchase Order {order.Number}"
        )), ct);

        foreach (var l in order.Lines)
        {
            l.BilledQuantity = l.Quantity;
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
