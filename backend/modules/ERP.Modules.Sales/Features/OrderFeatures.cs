using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
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
    decimal LineTotal);

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
    DateTime CreatedAt);

public sealed record OrderFulfillmentLineInput(Guid LineId, decimal DeliveredQuantity);

public sealed record GetOrder(Guid Id) : IQuery<OrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersRead; }
public sealed record CreateOrder(OrderInput Input) : ICommand<OrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
public sealed record UpdateOrder(Guid Id, OrderInput Input) : ICommand<OrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
public sealed record DeleteOrder(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
public sealed record ChangeOrderStatus(Guid Id, SalesOrderStatus Status) : ICommand<OrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
public sealed record UpdateOrderFulfillment(Guid Id, List<OrderFulfillmentLineInput> Lines) : ICommand<OrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }
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

internal sealed class OrderHandlers(
    IRepository<SalesOrder> orders,
    ISender sender,
    ILegalEntityGuard guard,
    INumberingService numbers,
    ICustomFieldService fields,
    IStockPostingService stockPosting) :
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

        if (order.Status == SalesOrderStatus.Invoiced)
            throw new ConflictException("Cannot edit an invoiced sales order.");

        var i = cmd.Input;
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

        await orders.SaveChangesAsync(ct);
        return await ToDtoAsync(order, fields, ct);
    }

    public async Task<Unit> Handle(DeleteOrder cmd, CancellationToken ct)
    {
        var order = await orders.GetForUpdateAsync(cmd.Id, ct);
        if (order.Status == SalesOrderStatus.Invoiced)
            throw new ConflictException("Cannot delete an invoiced sales order.");

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
            throw new ConflictException("Cannot change status of an already invoiced order.");
        if (order.Status == SalesOrderStatus.Cancelled)
            throw new ConflictException("Cannot change status of a cancelled order.");
        if (cmd.Status == SalesOrderStatus.Invoiced && !order.InvoicedInvoiceId.HasValue)
            throw new ConflictException("Cannot transition to Invoiced without converting to an accounting invoice.");

        order.Status = cmd.Status;
        await orders.SaveChangesAsync(ct);
        return await ToDtoAsync(order, fields, ct);
    }

    public async Task<OrderDto> Handle(UpdateOrderFulfillment cmd, CancellationToken ct)
    {
        var order = await orders.Tracked(new Specification<SalesOrder>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Sales order '{cmd.Id}' not found.");

        foreach (var ful in cmd.Lines)
        {
            var line = order.Lines.FirstOrDefault(l => l.Id == ful.LineId);
            if (line != null)
            {
                var targetQty = Math.Min(line.Quantity, Math.Max(0, ful.DeliveredQuantity));
                var delta = targetQty - line.DeliveredQuantity;
                if (delta > 0 && line.ItemId.HasValue)
                {
                    await stockPosting.IssueStockAsync(new StockIssueRequest(
                        order.LegalEntityId,
                        line.ItemId.Value,
                        delta,
                        DateOnly.FromDateTime(DateTime.UtcNow),
                        order.Number,
                        $"Delivery for SO {order.Number} ({line.ItemCode})"), ct);
                }
                line.DeliveredQuantity = targetQty;
            }
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

    public async Task<InvoiceDto> Handle(ConvertOrderToInvoice cmd, CancellationToken ct)
    {
        var order = await orders.Tracked(new Specification<SalesOrder>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Sales order '{cmd.Id}' not found.");

        if (order.InvoicedInvoiceId.HasValue)
        {
            return await sender.Send(new GetInvoice(order.InvoicedInvoiceId.Value), ct);
        }

        if (!order.CustomerPartyId.HasValue)
        {
            throw new ConflictException("Sales order must have an assigned Customer Party to generate an accounting invoice.");
        }

        var invoiceLines = order.Lines.Select(l => new InvoiceLineInput(
            Description: $"{order.Number}: {l.Description}",
            Quantity: l.Quantity,
            Price: Math.Round(l.UnitPrice * (1m - (l.DiscountPercent / 100m)), 4),
            ItemId: l.ItemId
        )).ToList();

        var invoice = await sender.Send(new CreateInvoice(new InvoiceInput(
            LegalEntityId: order.LegalEntityId,
            PartyId: order.CustomerPartyId.Value,
            InvoiceDate: DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate: order.ExpectedDeliveryDate,
            Lines: invoiceLines,
            Notes: $"Generated from Sales Order {order.Number}"
        )), ct);

        foreach (var l in order.Lines)
        {
            var undelivered = l.Quantity - l.DeliveredQuantity;
            if (undelivered > 0 && l.ItemId.HasValue)
            {
                await stockPosting.IssueStockAsync(new StockIssueRequest(
                    order.LegalEntityId,
                    l.ItemId.Value,
                    undelivered,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    order.Number,
                    $"Auto-fulfillment for Invoiced SO {order.Number} ({l.ItemCode})"), ct);
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
                l.InvoicedQuantity, l.UnitOfMeasure, l.UnitPrice, l.DiscountPercent, l.TaxRate, l.LineTotal)).ToList(),
            cf,
            o.CreatedAt);
    }
}
