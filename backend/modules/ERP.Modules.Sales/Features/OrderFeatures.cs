using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
using ERP.Modules.Sales.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Sales.Features;

public sealed record OrderLineInput(
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate);

public sealed record OrderInput(
    Guid LegalEntityId,
    string? Number,
    Guid? QuoteId,
    Guid? CustomerPartyId,
    string CustomerName,
    string? CustomerReference,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string Currency,
    string? PaymentTerms,
    string? ShippingAddress,
    string? Notes,
    List<OrderLineInput> Lines,
    JsonObject? CustomFields = null);

public sealed record OrderLineDto(
    Guid Id,
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    decimal DeliveredQuantity,
    decimal InvoicedQuantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate,
    decimal LineTotal,
    decimal ReturnedQuantity = 0);

public sealed record OrderSummaryDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid? QuoteId,
    string? QuoteNumber,
    Guid? CustomerPartyId,
    string CustomerName,
    string? CustomerReference,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string Currency,
    SalesOrderStatus Status,
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal TotalAmount,
    int LineCount,
    Guid? InvoicedInvoiceId,
    DateTime CreatedAt);

public sealed record OrderDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid? QuoteId,
    string? QuoteNumber,
    Guid? CustomerPartyId,
    string CustomerName,
    string? CustomerReference,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string Currency,
    SalesOrderStatus Status,
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal TotalAmount,
    string? PaymentTerms,
    string? ShippingAddress,
    string? Notes,
    Guid? InvoicedInvoiceId,
    IReadOnlyList<OrderLineDto> Lines,
    JsonObject CustomFields,
    DateTime CreatedAt,
    IReadOnlyList<string>? Warnings = null);

public sealed record OrderFulfillmentLineInput(Guid LineId, decimal DeliveredQuantity);

public sealed record GetOrder(Guid Id) : IQuery<OrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersRead; }
public sealed record CreateOrder(OrderInput Input) : ICommand<OrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
public sealed record UpdateOrder(Guid Id, OrderInput Input) : ICommand<OrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
public sealed record DeleteOrder(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
public sealed record ChangeOrderStatus(Guid Id, SalesOrderStatus Status) : ICommand<OrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
/// <summary>DeliveredQuantity is cumulative. DeliveryDate defaults to today and WarehouseId to the legal entity's first active warehouse.</summary>
public sealed record UpdateOrderFulfillment(Guid Id, List<OrderFulfillmentLineInput> Lines, DateOnly? DeliveryDate = null, Guid? WarehouseId = null) : ICommand<OrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
public sealed record ConvertOrderToInvoice(Guid Id) : ICommand<InvoiceDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }

public sealed class OrderInputValidator : AbstractValidator<OrderInput>
{
    public OrderInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Sales order must contain at least one line.");
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

public sealed class CreateOrderValidator : AbstractValidator<CreateOrder>
{
    public CreateOrderValidator() => RuleFor(x => x.Input).SetValidator(new OrderInputValidator());
}

public sealed class UpdateOrderValidator : AbstractValidator<UpdateOrder>
{
    public UpdateOrderValidator() => RuleFor(x => x.Input).SetValidator(new OrderInputValidator());
}

/// <summary>The customer and items a quote or order points at must exist in this workspace (the database cannot tell, its foreign keys ignore the workspace).</summary>
internal static class SalesReferences
{
    public static async Task EnsureAsync(IReferenceCheck references, Guid? customerPartyId, IEnumerable<Guid?> itemIds, CancellationToken ct)
    {
        if (customerPartyId is { } party && !await references.AllExistAsync<Party>([party], ct))
            throw new FieldValidationException("customerPartyId", "Customer not found.");
        if (!await references.AllExistAsync<Item>(itemIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList(), ct))
            throw new FieldValidationException("lines", "An item was not found.");
    }
}

public sealed class UpdateOrderFulfillmentValidator : AbstractValidator<UpdateOrderFulfillment>
{
    public UpdateOrderFulfillmentValidator()
    {
        RuleFor(x => x.DeliveryDate).MustBePlausibleDate();
        RuleForEach(x => x.Lines).ChildRules(l => l.RuleFor(x => x.DeliveredQuantity).GreaterThanOrEqualTo(0).MustHaveAtMostDecimals(4));
    }
}

internal sealed class OrderHandlers(
    IRepository<SalesOrder> orders,
    ISender sender,
    ILegalEntityGuard guard,
    INumberingService numbers,
    ICustomFieldService fields,
    IStockPostingService stockPosting,
    IReadRepository<TaxCode> taxCodes,
    IUnitOfWork uow,
    IReferenceCheck references,
    ICreditLimitService creditLimits) :
    IRequestHandler<GetOrder, OrderDto>,
    IRequestHandler<CreateOrder, OrderDto>,
    IRequestHandler<UpdateOrder, OrderDto>,
    IRequestHandler<DeleteOrder, Unit>,
    IRequestHandler<ChangeOrderStatus, OrderDto>,
    IRequestHandler<UpdateOrderFulfillment, OrderDto>,
    IRequestHandler<ConvertOrderToInvoice, InvoiceDto>
{
    public async Task<OrderDto> Handle(GetOrder q, CancellationToken ct)
    {
        var order = await orders.Tracked(new Specification<SalesOrder>(x => x.Id == q.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Sales order '{q.Id}' not found.");
        return await ToDtoAsync(order, fields, ct);
    }

    public async Task<OrderDto> Handle(CreateOrder cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        await SalesReferences.EnsureAsync(references, i.CustomerPartyId, i.Lines.Select(l => l.ItemId), ct);
        if (i.QuoteId is { } quoteId && !await references.AllExistAsync<SalesQuote>([quoteId], ct))
            throw new FieldValidationException("quoteId", "Quote not found.");
        var order = new SalesOrder
        {
            LegalEntityId = entity.Id,
            QuoteId = i.QuoteId,
            CustomerPartyId = i.CustomerPartyId,
            CustomerName = i.CustomerName.Trim(),
            CustomerReference = i.CustomerReference?.Trim(),
            OrderDate = i.OrderDate,
            ExpectedDeliveryDate = i.ExpectedDeliveryDate,
            Currency = i.Currency.Trim().ToUpperInvariant(),
            PaymentTerms = i.PaymentTerms,
            ShippingAddress = i.ShippingAddress,
            Notes = i.Notes,
            Status = SalesOrderStatus.Draft,
        };

        order.Number = string.IsNullOrWhiteSpace(i.Number)
            ? await numbers.NextAsync(Docs.SalesOrder, entity.Id, i.OrderDate, ct)
            : i.Number.Trim();

        if (await orders.AnyAsync(x => x.Number == order.Number, ct))
            throw new ConflictException($"Sales order number '{order.Number}' already exists.");

        PopulateLinesAndTotals(order, i.Lines);
        await fields.ApplyAsync(Docs.SalesOrder, order, i.CustomFields, isCreate: true, ct);

        orders.Add(order);
        await orders.SaveChangesAsync(ct);
        return await ToDtoAsync(order, fields, ct);
    }

    public async Task<OrderDto> Handle(UpdateOrder cmd, CancellationToken ct)
    {
        var order = await orders.Tracked(new Specification<SalesOrder>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Sales order '{cmd.Id}' not found.");

        var canEdit = order.Status == SalesOrderStatus.Draft ||
                      (order.Status == SalesOrderStatus.Confirmed && order.Lines.All(l => l.DeliveredQuantity == 0));
        if (!canEdit)
            throw new ConflictException($"Cannot edit sales order in status '{order.Status}'" + (order.Lines.Any(l => l.DeliveredQuantity > 0) ? " with delivered quantities." : "."));

        var i = cmd.Input;
        await SalesReferences.EnsureAsync(references, i.CustomerPartyId, i.Lines.Select(l => l.ItemId), ct);
        order.CustomerPartyId = i.CustomerPartyId;
        order.CustomerName = i.CustomerName.Trim();
        order.CustomerReference = i.CustomerReference?.Trim();
        order.OrderDate = i.OrderDate;
        order.ExpectedDeliveryDate = i.ExpectedDeliveryDate;
        order.Currency = i.Currency.Trim().ToUpperInvariant();
        order.PaymentTerms = i.PaymentTerms;
        order.ShippingAddress = i.ShippingAddress;
        order.Notes = i.Notes;

        PopulateLinesAndTotals(order, i.Lines);
        await fields.ApplyAsync(Docs.SalesOrder, order, i.CustomFields, isCreate: false, ct);
        var warning = order.Status == SalesOrderStatus.Confirmed ? await CheckCreditAsync(order, orders, creditLimits, ct) : null; // a confirmed order can grow past the limit

        await orders.SaveChangesAsync(ct);
        return await ToDtoAsync(order, fields, ct) with { Warnings = warning is null ? null : [warning] };
    }

    public async Task<Unit> Handle(DeleteOrder cmd, CancellationToken ct)
    {
        var order = await orders.Tracked(new Specification<SalesOrder>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Sales order '{cmd.Id}' not found.");
        if (order.Status == SalesOrderStatus.Invoiced)
            throw new ConflictException("Cannot delete an invoiced sales order.");
        if (order.Lines.Any(l => l.DeliveredQuantity > 0))
            throw new ConflictException("This sales order has delivered goods: stock has left and its cost is posted, so it cannot be deleted. Record a sales return first.");

        orders.Remove(order);
        await orders.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<OrderDto> Handle(ChangeOrderStatus cmd, CancellationToken ct)
    {
        var order = await orders.Tracked(new Specification<SalesOrder>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Sales order '{cmd.Id}' not found.");

        if (order.Status == SalesOrderStatus.Invoiced)
            throw new ConflictException("An invoiced sales order is reopened by voiding its invoice.");
        SalesTransitions.Order.Ensure(order.Status, cmd.Status, isSystem: false);
        if (cmd.Status == SalesOrderStatus.Cancelled && order.Lines.Any(l => l.DeliveredQuantity > 0))
            throw new ConflictException("This sales order has delivered goods: stock has left and its cost is posted, so it cannot be cancelled. Record a sales return first.");
        var warning = cmd.Status == SalesOrderStatus.Confirmed ? await CheckCreditAsync(order, orders, creditLimits, ct) : null;

        order.Status = cmd.Status;
        await orders.SaveChangesAsync(ct);
        return await ToDtoAsync(order, fields, ct) with { Warnings = warning is null ? null : [warning] };
    }

    /// <summary>A confirmed order counts toward the customer's credit together with their other confirmed orders not invoiced yet.</summary>
    public static async Task<string?> CheckCreditAsync(SalesOrder order, IReadRepository<SalesOrder> orders, ICreditLimitService creditLimits, CancellationToken ct)
    {
        if (order.CustomerPartyId is not { } party) return null;
        var open = await orders.Query(new Specification<SalesOrder>(o => o.CustomerPartyId == party && o.LegalEntityId == order.LegalEntityId && o.Id != order.Id
                && (o.Status == SalesOrderStatus.Confirmed || o.Status == SalesOrderStatus.Processing || o.Status == SalesOrderStatus.Delivered)))
            .Select(o => o.TotalAmount).ToListAsync(ct);
        return await creditLimits.CheckAsync(party, order.LegalEntityId, order.TotalAmount + open.Sum(), ct);
    }

    // Stock issues, status and totals commit together: a failure part-way leaves no half-fulfilled order.
    public Task<OrderDto> Handle(UpdateOrderFulfillment cmd, CancellationToken ct) => uow.InTransactionAsync(token => FulfillAsync(cmd, token), ct);

    private async Task<OrderDto> FulfillAsync(UpdateOrderFulfillment cmd, CancellationToken ct)
    {
        var order = await orders.Tracked(new Specification<SalesOrder>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Sales order '{cmd.Id}' not found.");

        if (order.Status != SalesOrderStatus.Confirmed && order.Status != SalesOrderStatus.Processing)
            throw new ConflictException($"Cannot fulfill sales order in status '{order.Status}'. Only Confirmed or Processing orders can be fulfilled.");

        var deliveryDate = cmd.DeliveryDate ?? DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        foreach (var ful in cmd.Lines)
        {
            var line = order.Lines.FirstOrDefault(l => l.Id == ful.LineId)
                ?? throw new FieldValidationException("lines", $"Line '{ful.LineId}' is not on sales order '{order.Number}'.");
            if (ful.DeliveredQuantity < line.DeliveredQuantity)
                throw new FieldValidationException("deliveredQuantity", $"Delivered quantity ({ful.DeliveredQuantity}) cannot be less than current delivered quantity ({line.DeliveredQuantity}).");
            if (ful.DeliveredQuantity > line.Quantity)
                throw new FieldValidationException("deliveredQuantity", $"Delivered quantity ({ful.DeliveredQuantity}) cannot be more than the {line.Quantity} ordered on '{line.Description}'.");

            var delta = ful.DeliveredQuantity - line.DeliveredQuantity;
            if (delta > 0 && line.ItemId.HasValue)
            {
                await stockPosting.IssueStockAsync(new StockIssueRequest(
                    order.LegalEntityId,
                    line.ItemId.Value,
                    delta,
                    deliveryDate,
                    order.Number,
                    $"Delivery for SO {order.Number} ({line.ItemCode})",
                    cmd.WarehouseId,
                    EnforceAvailable: true), ct);
            }
            line.DeliveredQuantity = ful.DeliveredQuantity;
        }

        var allDelivered = order.Lines.All(l => l.DeliveredQuantity >= l.Quantity);
        var anyDelivered = order.Lines.Any(l => l.DeliveredQuantity > 0);

        if (allDelivered && order.Status != SalesOrderStatus.Invoiced)
        {
            order.Status = SalesOrderStatus.Delivered;
        }
        else if (anyDelivered && order.Status == SalesOrderStatus.Confirmed)
        {
            order.Status = SalesOrderStatus.Processing;
        }

        await orders.SaveChangesAsync(ct);
        return await ToDtoAsync(order, fields, ct);
    }

    // Invoice, auto-fulfilment stock issue and order status commit together.
    public Task<InvoiceDto> Handle(ConvertOrderToInvoice cmd, CancellationToken ct) => uow.InTransactionAsync(token => ConvertAsync(cmd, token), ct);

    private async Task<InvoiceDto> ConvertAsync(ConvertOrderToInvoice cmd, CancellationToken ct)
    {
        var order = await orders.Tracked(new Specification<SalesOrder>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Sales order '{cmd.Id}' not found.");

        if (order.InvoicedInvoiceId.HasValue)
        {
            return await sender.Send(new GetInvoice(order.InvoicedInvoiceId.Value), ct);
        }

        SalesTransitions.Order.Ensure(order.Status, SalesOrderStatus.Invoiced, isSystem: true);

        if (!order.CustomerPartyId.HasValue)
        {
            throw new ConflictException("Sales order must have an assigned Customer Party to generate an accounting invoice.");
        }

        var entity = await guard.RequireAsync(order.LegalEntityId, ct);
        if (!string.Equals(order.Currency, entity.BaseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("Foreign-currency sales orders cannot be invoiced yet.");
        }

        var activeTaxCodes = await taxCodes.Query(new Specification<TaxCode>(t =>
            t.IsActive && (t.LegalEntityId == order.LegalEntityId || t.LegalEntityId == null)))
            .ToListAsync(ct);

        var invoiceLines = new List<InvoiceLineInput>();
        foreach (var l in order.Lines)
        {
            Guid? taxCodeId = null;
            if (l.TaxRate > 0)
            {
                var matchedCode = activeTaxCodes.FirstOrDefault(t => t.LegalEntityId == order.LegalEntityId && t.Rate == l.TaxRate)
                    ?? activeTaxCodes.FirstOrDefault(t => t.LegalEntityId == null && t.Rate == l.TaxRate);

                if (matchedCode is null)
                {
                    throw new ConflictException($"No active tax code found for line '{l.Description}' with rate {l.TaxRate}%.");
                }
                taxCodeId = matchedCode.Id;
            }

            invoiceLines.Add(new InvoiceLineInput(
                Description: $"{order.Number}: {l.Description}",
                Quantity: l.Quantity,
                Price: Math.Round(l.UnitPrice * (1m - (l.DiscountPercent / 100m)), 4),
                TaxCodeId: taxCodeId,
                ItemId: l.ItemId
            ));
        }

        var invoice = await sender.Send(new CreateInvoice(new InvoiceInput(
            LegalEntityId: order.LegalEntityId,
            PartyId: order.CustomerPartyId.Value,
            InvoiceDate: DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow),
            DueDate: null,
            Lines: invoiceLines,
            Notes: $"Generated from Sales Order {order.Number}",
            SourceType: "salesorder",
            SourceId: order.Id
        )), ct);

        var maxAllowedDiff = 0.01m * Math.Max(1, order.Lines.Count);
        if (Math.Abs(invoice.Total - order.TotalAmount) > maxAllowedDiff)
        {
            await sender.Send(new DeleteInvoice(invoice.Id), ct);
            throw new ConflictException($"Invoice total {invoice.Total} does not match sales order total {order.TotalAmount}.");
        }

        foreach (var l in order.Lines)
        {
            var undelivered = l.Quantity - l.DeliveredQuantity;
            if (undelivered > 0 && l.ItemId.HasValue)
            {
                await stockPosting.IssueStockAsync(new StockIssueRequest(
                    order.LegalEntityId,
                    l.ItemId.Value,
                    undelivered,
                    DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow),
                    order.Number,
                    $"Auto-fulfillment for Invoiced SO {order.Number} ({l.ItemCode})",
                    EnforceAvailable: true), ct);
                l.AutoDeliveredQuantity = undelivered;
                l.DeliveredQuantity = l.Quantity;
            }
            l.InvoicedQuantity = l.Quantity;
        }

        order.Status = SalesOrderStatus.Invoiced;
        order.InvoicedInvoiceId = invoice.Id;
        await orders.SaveChangesAsync(ct);

        return invoice;
    }

    private static void PopulateLinesAndTotals(SalesOrder order, List<OrderLineInput> lines)
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

            order.Lines.Add(new SalesOrderLine
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

    public static async Task<OrderDto> ToDtoAsync(SalesOrder o, ICustomFieldService fields, CancellationToken ct)
    {
        var cf = await fields.ReadAsync(Docs.SalesOrder, o, ct);
        return new OrderDto(
            o.Id,
            o.Number,
            o.LegalEntityId,
            o.QuoteId,
            o.QuoteNumber,
            o.CustomerPartyId,
            o.CustomerName,
            o.CustomerReference,
            o.OrderDate,
            o.ExpectedDeliveryDate,
            o.Currency,
            o.Status,
            o.SubTotal,
            o.TaxTotal,
            o.DiscountTotal,
            o.TotalAmount,
            o.PaymentTerms,
            o.ShippingAddress,
            o.Notes,
            o.InvoicedInvoiceId,
            o.Lines.Select(l => new OrderLineDto(
                l.Id, l.ItemId, l.ItemCode, l.Description, l.Quantity, l.DeliveredQuantity,
                l.InvoicedQuantity, l.UnitOfMeasure, l.UnitPrice, l.DiscountPercent, l.TaxRate, l.LineTotal, l.ReturnedQuantity)).ToList(),
            cf,
            o.CreatedAt);
    }
}
