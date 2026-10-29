using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Catalog;
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
    private readonly IStockPostingService _stockPosting;
    private readonly IPostingService _posting;
    private readonly IReadRepository<Item> _items;
    private readonly ICurrentUser _currentUser;

    public OrderHandlers(
        IRepository<PosOrder> orders,
        IRepository<PosSession> sessions,
        INumberingService numbering,
        IStockPostingService stockPosting,
        IPostingService posting,
        IReadRepository<Item> items,
        ICurrentUser currentUser)
    {
        _orders = orders;
        _sessions = sessions;
        _numbering = numbering;
        _stockPosting = stockPosting;
        _posting = posting;
        _items = items;
        _currentUser = currentUser;
    }

    public async Task<PosOrderDto> Handle(CreatePosOrderCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessions.GetForUpdateAsync(request.SessionId, cancellationToken);

        if (session.Status != PosSessionStatus.Open)
        {
            throw new ConflictException("Cannot place POS orders in a closed session.");
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

        var paymentsSum = request.Payments.Sum(p => p.Amount);
        if (Math.Abs(paymentsSum - totalAmount) > 0.05m)
        {
            throw new ConflictException($"Payments total ({paymentsSum:N2}) does not match order total ({totalAmount:N2}).");
        }
        foreach (var p in request.Payments)
        {
            if (p.TenderedAmount > 0 && p.TenderedAmount < p.Amount)
            {
                throw new ConflictException($"Tendered amount ({p.TenderedAmount:N2}) cannot be less than payment amount ({p.Amount:N2}).");
            }
        }

        var payments = request.Payments.Select(p => new PosPayment
        {
            Method = p.Method,
            Amount = p.Amount,
            TenderedAmount = p.TenderedAmount,
            ChangeAmount = p.ChangeAmount,
            Reference = p.Reference,
            PaymentDate = DateTimeOffset.UtcNow
        }).ToList();

        var cashierId = (_currentUser.UserId.HasValue && _currentUser.UserId.Value != Guid.Empty)
            ? _currentUser.UserId.Value
            : (request.CashierUserId != Guid.Empty ? request.CashierUserId : Guid.NewGuid());
        var cashierName = !string.IsNullOrWhiteSpace(_currentUser.Email)
            ? _currentUser.Email
            : (!string.IsNullOrWhiteSpace(request.CashierName) ? request.CashierName.Trim() : "POS Cashier");

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
            CashierUserId = cashierId,
            CashierName = cashierName,
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

        // Post accounting journal for revenue & tax
        try
        {
            var salesAccount = await _posting.AccountForRoleAsync("income.sales", cancellationToken);
            var taxAccount = taxTotal > 0 ? await _posting.AccountForRoleAsync(SystemRoles.TaxOutput, cancellationToken) : Guid.Empty;
            var cashAccount = await _posting.AccountForRoleAsync("cash", cancellationToken);
            var bankAccount = await _posting.AccountForRoleAsync("bank", cancellationToken);

            var postingLines = new List<PostingLine>();
            foreach (var p in payments)
            {
                var acct = p.Method == PosPaymentMethod.Cash ? cashAccount : bankAccount;
                postingLines.Add(new PostingLine(acct, p.Amount, 0, $"POS payment ({p.Method}) - {order.Number}"));
            }
            postingLines.Add(new PostingLine(salesAccount, 0, subtotal, $"POS sales revenue - {order.Number}"));
            if (taxTotal > 0 && taxAccount != Guid.Empty)
            {
                postingLines.Add(new PostingLine(taxAccount, 0, taxTotal, $"POS tax output - {order.Number}"));
            }

            await _posting.PostAsync(new PostingRequest(
                order.LegalEntityId,
                DateOnly.FromDateTime(order.OrderDate.UtcDateTime),
                "pos",
                order.Id,
                $"POS Order {order.Number}",
                order.Number,
                postingLines), cancellationToken);
        }
        catch
        {
            // Optional GL post fallback
        }

        foreach (var l in lines)
        {
            if (l.ItemId.HasValue && l.Quantity > 0)
            {
                await _stockPosting.IssueStockAsync(new StockIssueRequest(
                    order.LegalEntityId,
                    l.ItemId.Value,
                    l.Quantity,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    order.Number,
                    $"POS sale {order.Number} ({l.ItemCode})"), cancellationToken);
            }
        }

        return Map(order);
    }

    public async Task<PosOrderDto> Handle(VoidPosOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orders.Tracked(new Specification<PosOrder>(x => x.Id == request.OrderId))
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Order {request.OrderId} not found");

        if (order.Status == PosOrderStatus.Voided)
        {
            throw new ConflictException("Order is already voided.");
        }

        order.Status = PosOrderStatus.Voided;
        order.Notes = string.IsNullOrWhiteSpace(order.Notes)
            ? $"Voided: {request.Reason}"
            : $"{order.Notes} | Voided: {request.Reason}";

        var session = await _sessions.GetForUpdateAsync(order.SessionId, cancellationToken);
        if (session != null && session.Status == PosSessionStatus.Open)
        {
            decimal cashRefunded = 0;
            foreach (var p in order.Payments)
            {
                if (p.Method == PosPaymentMethod.Cash)
                {
                    cashRefunded += (p.Amount - p.ChangeAmount);
                }
            }

            session.TotalReturns += order.TotalAmount;
            session.TotalSales -= order.TotalAmount;
            session.ExpectedCash -= cashRefunded;
            await _sessions.SaveChangesAsync(cancellationToken);
        }

        await _orders.SaveChangesAsync(cancellationToken);

        foreach (var l in order.Lines)
        {
            if (l.ItemId.HasValue && l.Quantity > 0)
            {
                var item = await _items.FindAsync(l.ItemId.Value, cancellationToken);
                var costPrice = item?.PurchasePrice ?? 0m;

                await _stockPosting.ReceiveStockAsync(new StockReceiveRequest(
                    order.LegalEntityId,
                    l.ItemId.Value,
                    l.Quantity,
                    costPrice,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    order.Number,
                    $"POS void return {order.Number} ({l.ItemCode})"), cancellationToken);
            }
        }

        return Map(order);
    }

    public async Task<PosOrderDto> Handle(GetPosOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _orders.Query(new Specification<PosOrder>(x => x.Id == request.Id))
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Order {request.Id} not found");

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
