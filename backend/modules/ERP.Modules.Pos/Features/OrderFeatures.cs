using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Pos.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Features;

public record PosOrderLineInput(
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate);

public record PosPaymentInput(
    PosPaymentMethod Method,
    decimal Amount,
    decimal TenderedAmount,
    decimal ChangeAmount,
    string? Reference);

public record CreatePosOrderCommand(
    Guid LegalEntityId,
    Guid RegisterId,
    Guid SessionId,
    Guid? CustomerPartyId,
    string? CustomerName,
    Guid CashierUserId,
    string CashierName,
    List<PosOrderLineInput> Lines,
    List<PosPaymentInput> Payments,
    string? Notes,
    string? CustomFields) : ICommand<PosOrderDto>;

public record VoidPosOrderCommand(
    Guid OrderId,
    string Reason) : ICommand<PosOrderDto>;

public record GetPosOrderByIdQuery(Guid Id) : IQuery<PosOrderDto>;

public record PosOrderLineDto(
    Guid Id,
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate,
    decimal LineTotal);

public record PosPaymentDto(
    Guid Id,
    PosPaymentMethod Method,
    decimal Amount,
    decimal TenderedAmount,
    decimal ChangeAmount,
    string? Reference,
    DateTimeOffset PaymentDate);

public record PosOrderSummaryDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid RegisterId,
    Guid SessionId,
    Guid? CustomerPartyId,
    string CustomerName,
    DateTimeOffset OrderDate,
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal TotalAmount,
    PosOrderStatus Status,
    PosPaymentStatus PaymentStatus,
    string CashierName,
    int LineCount,
    DateTimeOffset CreatedAt);

public record PosOrderDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid RegisterId,
    Guid SessionId,
    Guid? CustomerPartyId,
    string CustomerName,
    DateTimeOffset OrderDate,
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal TotalAmount,
    PosOrderStatus Status,
    PosPaymentStatus PaymentStatus,
    Guid CashierUserId,
    string CashierName,
    string? Notes,
    string? CustomFields,
    List<PosOrderLineDto> Lines,
    List<PosPaymentDto> Payments,
    DateTimeOffset CreatedAt);

public class CreatePosOrderValidator : AbstractValidator<CreatePosOrderCommand>
{
    public CreatePosOrderValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.RegisterId).NotEmpty();
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one item is required.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ItemCode).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0);
            l.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        });
        RuleFor(x => x.Payments).NotEmpty().WithMessage("At least one payment tender is required.");
    }
}

public class OrderHandlers :
    IRequestHandler<CreatePosOrderCommand, PosOrderDto>,
    IRequestHandler<VoidPosOrderCommand, PosOrderDto>,
    IRequestHandler<GetPosOrderByIdQuery, PosOrderDto>
{
    private readonly IRepository<PosOrder> _orders;
    private readonly IRepository<PosSession> _sessions;
    private readonly INumberingService _numbering;

    public OrderHandlers(
        IRepository<PosOrder> orders,
        IRepository<PosSession> sessions,
        INumberingService numbering)
    {
        _orders = orders;
        _sessions = sessions;
        _numbering = numbering;
    }

    public async Task<PosOrderDto> Handle(CreatePosOrderCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessions.GetForUpdateAsync(request.SessionId, cancellationToken);

        if (session.Status != PosSessionStatus.Open)
        {
            throw new InvalidOperationException("Cannot place POS orders in a closed session.");
        }

        var orderNumber = await _numbering.NextAsync(
            "POS",
            request.LegalEntityId,
            null,
            cancellationToken);

        decimal subtotal = 0;
        decimal taxTotal = 0;
        decimal discountTotal = 0;

        var lines = new List<PosOrderLine>();
        foreach (var l in request.Lines)
        {
            var rawLine = l.Quantity * l.UnitPrice;
            var disc = rawLine * (l.DiscountPercent / 100m);
            var afterDisc = rawLine - disc;
            var tax = afterDisc * (l.TaxRate / 100m);
            var lineTotal = afterDisc + tax;

            subtotal += afterDisc;
            taxTotal += tax;
            discountTotal += disc;

            lines.Add(new PosOrderLine
            {
                ItemId = l.ItemId,
                ItemCode = l.ItemCode.Trim(),
                Description = l.Description.Trim(),
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                DiscountPercent = l.DiscountPercent,
                TaxRate = l.TaxRate,
                LineTotal = lineTotal
            });
        }

        var totalAmount = subtotal + taxTotal;

        var payments = request.Payments.Select(p => new PosPayment
        {
            Method = p.Method,
            Amount = p.Amount,
            TenderedAmount = p.TenderedAmount,
            ChangeAmount = p.ChangeAmount,
            Reference = p.Reference,
            PaymentDate = DateTimeOffset.UtcNow
        }).ToList();

        var order = new PosOrder
        {
            Number = orderNumber,
            LegalEntityId = request.LegalEntityId,
            RegisterId = request.RegisterId,
            SessionId = request.SessionId,
            CustomerPartyId = request.CustomerPartyId,
            CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? "Walk-in Customer" : request.CustomerName.Trim(),
            OrderDate = DateTimeOffset.UtcNow,
            SubTotal = subtotal,
            TaxTotal = taxTotal,
            DiscountTotal = discountTotal,
            TotalAmount = totalAmount,
            Status = PosOrderStatus.Completed,
            PaymentStatus = PosPaymentStatus.Paid,
            CashierUserId = request.CashierUserId,
            CashierName = request.CashierName.Trim(),
            Notes = request.Notes,
            CustomFields = request.CustomFields,
            Lines = lines,
            Payments = payments
        };

        _orders.Add(order);

        decimal cashPaid = 0;
        foreach (var p in payments)
        {
            if (p.Method == PosPaymentMethod.Cash)
            {
                cashPaid += (p.Amount - p.ChangeAmount);
            }
        }

        // Update session totals
        session.TotalSales += totalAmount;
        session.ExpectedCash += cashPaid;

        await _orders.SaveChangesAsync(cancellationToken);
        await _sessions.SaveChangesAsync(cancellationToken);

        return Map(order);
    }

    public async Task<PosOrderDto> Handle(VoidPosOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orders.Tracked(new Specification<PosOrder>(x => x.Id == request.OrderId))
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Order {request.OrderId} not found");

        if (order.Status == PosOrderStatus.Voided)
        {
            throw new InvalidOperationException("Order is already voided.");
        }

        order.Status = PosOrderStatus.Voided;
        order.Notes = string.IsNullOrWhiteSpace(order.Notes)
            ? $"Voided: {request.Reason}"
            : $"{order.Notes} | Voided: {request.Reason}";

        var session = await _sessions.GetForUpdateAsync(order.SessionId, cancellationToken);
        if (session != null)
        {
            session.TotalReturns += order.TotalAmount;
            session.TotalSales -= order.TotalAmount;
            await _sessions.SaveChangesAsync(cancellationToken);
        }

        await _orders.SaveChangesAsync(cancellationToken);

        return Map(order);
    }

    public async Task<PosOrderDto> Handle(GetPosOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _orders.Query(new Specification<PosOrder>(x => x.Id == request.Id))
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Order {request.Id} not found");

        return Map(order);
    }

    private static PosOrderDto Map(PosOrder o) => new(
        o.Id,
        o.Number,
        o.LegalEntityId,
        o.RegisterId,
        o.SessionId,
        o.CustomerPartyId,
        o.CustomerName,
        o.OrderDate,
        o.SubTotal,
        o.TaxTotal,
        o.DiscountTotal,
        o.TotalAmount,
        o.Status,
        o.PaymentStatus,
        o.CashierUserId,
        o.CashierName,
        o.Notes,
        o.CustomFields,
        o.Lines.Select(l => new PosOrderLineDto(
            l.Id,
            l.ItemId,
            l.ItemCode,
            l.Description,
            l.Quantity,
            l.UnitPrice,
            l.DiscountPercent,
            l.TaxRate,
            l.LineTotal)).ToList(),
        o.Payments.Select(p => new PosPaymentDto(
            p.Id,
            p.Method,
            p.Amount,
            p.TenderedAmount,
            p.ChangeAmount,
            p.Reference,
            p.PaymentDate)).ToList(),
        o.CreatedAt);
}
