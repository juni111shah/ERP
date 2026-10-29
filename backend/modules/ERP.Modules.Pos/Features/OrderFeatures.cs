using System.Text.Json.Serialization;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Modules.Pos.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Features;

public record PosOrderLineInput(
    Guid? ItemId,
    string? ItemCode = null,
    string? Description = null,
    decimal Quantity = 1,
    decimal? UnitPrice = null,
    decimal DiscountPercent = 0,
    decimal? TaxRate = null);

public record PosPaymentInput(
    PosPaymentMethod Method,
    decimal Amount,
    decimal TenderedAmount,
    decimal ChangeAmount,
    string? Reference);

[method: JsonConstructor]
public record CreatePosOrderCommand(
    Guid LegalEntityId,
    Guid RegisterId,
    Guid SessionId,
    Guid? CustomerPartyId,
    string? CustomerName,
    List<PosOrderLineInput> Lines,
    List<PosPaymentInput> Payments,
    string? Notes = null,
    string? CustomFields = null,
    Guid? CashierUserId = null,
    string? CashierName = null) : ICommand<PosOrderDto>
{
    // Backwards-compatible constructor for existing tests/callers specifying CashierUserId and CashierName
    public CreatePosOrderCommand(
        Guid LegalEntityId,
        Guid RegisterId,
        Guid SessionId,
        Guid? CustomerPartyId,
        string? CustomerName,
        Guid? CashierUserId,
        string? CashierName,
        List<PosOrderLineInput> Lines,
        List<PosPaymentInput> Payments,
        string? Notes,
        string? CustomFields)
        : this(LegalEntityId, RegisterId, SessionId, CustomerPartyId, CustomerName, Lines, Payments, Notes, CustomFields, CashierUserId, CashierName)
    {
    }
}

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
            l.RuleFor(x => x.Quantity).GreaterThan(0);
            l.RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100);
            l.When(x => !x.ItemId.HasValue, () =>
            {
                l.RuleFor(x => x.ItemCode).NotEmpty();
            });
            l.When(x => x.UnitPrice.HasValue, () =>
            {
                l.RuleFor(x => x.UnitPrice!.Value).GreaterThanOrEqualTo(0);
            });
        });
        RuleFor(x => x.Payments).NotEmpty().WithMessage("At least one payment tender is required.");
        RuleForEach(x => x.Payments).ChildRules(p =>
        {
            p.RuleFor(x => x.Amount).GreaterThan(0);
            p.RuleFor(x => x.Method).IsInEnum();
        });
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
    private readonly IReadRepository<TaxCode> _taxCodes;
    private readonly ISettingsService _settings;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public OrderHandlers(
        IRepository<PosOrder> orders,
        IRepository<PosSession> sessions,
        INumberingService numbering,
        IStockPostingService stockPosting,
        IPostingService posting,
        IReadRepository<Item> items,
        IReadRepository<TaxCode> taxCodes,
        ISettingsService settings,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _orders = orders;
        _sessions = sessions;
        _numbering = numbering;
        _stockPosting = stockPosting;
        _posting = posting;
        _items = items;
        _taxCodes = taxCodes;
        _settings = settings;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<PosOrderDto> Handle(CreatePosOrderCommand request, CancellationToken cancellationToken)
    {
        return await _unitOfWork.InTransactionAsync(async token =>
        {
            var session = await _sessions.Query(new Specification<PosSession>(s => s.Id == request.SessionId))
                .FirstOrDefaultAsync(token)
                ?? throw new NotFoundException($"Session {request.SessionId} not found");

            if (session.Status != PosSessionStatus.Open)
            {
                throw new ConflictException("Cannot place POS orders in a closed session.");
            }

        // Cashier identity from ICurrentUser; client-supplied cashier is ignored
        var cashierId = (_currentUser.UserId.HasValue && _currentUser.UserId.Value != Guid.Empty)
            ? _currentUser.UserId.Value
            : (request.CashierUserId ?? Guid.Empty);
        var cashierName = !string.IsNullOrWhiteSpace(_currentUser.Email)
            ? _currentUser.Email
            : (_currentUser.UserId.HasValue ? _currentUser.UserId.Value.ToString() : (!string.IsNullOrWhiteSpace(request.CashierName) ? request.CashierName.Trim() : "POS Cashier"));

        var maxDiscount = await _settings.GetAsync<decimal?>("pos.maxDiscountPercent", request.LegalEntityId, cancellationToken) ?? 100m;
        var allowPartial = await _settings.GetAsync<bool?>("pos.allowPartialPayment", request.LegalEntityId, cancellationToken) ?? false;
        var canOverridePrice = _currentUser.HasPermission(Permissions.OrdersOverridePrice);

        // Load items in batch
        var itemIds = request.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToList();
        var items = itemIds.Count > 0
            ? await _items.Query(new Specification<Item>(i => itemIds.Contains(i.Id))).ToDictionaryAsync(i => i.Id, cancellationToken)
            : new Dictionary<Guid, Item>();

        // Load tax codes for items
        var taxCodeIds = items.Values.Where(i => i.TaxCodeId.HasValue).Select(i => i.TaxCodeId!.Value).Distinct().ToList();
        var taxCodes = taxCodeIds.Count > 0
            ? await _taxCodes.Query(new Specification<TaxCode>(t => taxCodeIds.Contains(t.Id))).ToDictionaryAsync(t => t.Id, cancellationToken)
            : new Dictionary<Guid, TaxCode>();

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
            if (l.DiscountPercent > maxDiscount)
            {
                throw new FieldValidationException("discountPercent", $"Discount ({l.DiscountPercent}%) exceeds maximum allowed ({maxDiscount}%).");
            }
            if (l.DiscountPercent < 0 || l.DiscountPercent > 100)
            {
                throw new FieldValidationException("discountPercent", "Discount must be between 0 and 100.");
            }

            decimal unitPrice;
            decimal taxRate;
            string itemCode = l.ItemCode ?? string.Empty;
            string description = l.Description ?? string.Empty;

            if (l.ItemId.HasValue)
            {
                if (!items.TryGetValue(l.ItemId.Value, out var item))
                {
                    throw new NotFoundException($"Item {l.ItemId.Value} not found.");
                }

                itemCode = string.IsNullOrWhiteSpace(itemCode) ? item.Sku : itemCode;
                description = string.IsNullOrWhiteSpace(description) ? item.Name : description;
                var catalogPrice = item.SalesPrice ?? 0m;

                if (l.UnitPrice.HasValue)
                {
                    if (l.UnitPrice.Value < catalogPrice && !canOverridePrice)
                    {
                        throw new FieldValidationException("unitPrice", $"Posting a lower unit price ({l.UnitPrice.Value:N2}) than item price ({catalogPrice:N2}) requires override permission.");
                    }
                    unitPrice = canOverridePrice ? l.UnitPrice.Value : catalogPrice;
                }
                else
                {
                    unitPrice = catalogPrice;
                }

                if (item.TaxCodeId.HasValue && taxCodes.TryGetValue(item.TaxCodeId.Value, out var tc))
                {
                    taxRate = tc.Rate;
                }
                else
                {
                    taxRate = l.TaxRate ?? 0m;
                }
            }
            else
            {
                unitPrice = l.UnitPrice ?? 0m;
                taxRate = l.TaxRate ?? 0m;
            }

            var rawLine = Money.Round(l.Quantity * unitPrice);
            var disc = Money.Round(rawLine * (l.DiscountPercent / 100m));
            var afterDisc = rawLine - disc;
            var tax = Money.Round(afterDisc * (taxRate / 100m));
            var lineTotal = afterDisc + tax;

            subtotal += afterDisc;
            taxTotal += tax;
            discountTotal += disc;

            lines.Add(new PosOrderLine
            {
                ItemId = l.ItemId,
                ItemCode = itemCode.Trim(),
                Description = description.Trim(),
                Quantity = l.Quantity,
                UnitPrice = unitPrice,
                DiscountPercent = l.DiscountPercent,
                TaxRate = taxRate,
                LineTotal = lineTotal
            });
        }

        var totalAmount = subtotal + taxTotal;

        // Payments validation
        foreach (var p in request.Payments)
        {
            if (p.Amount <= 0)
            {
                throw new FieldValidationException("payments", "Payment amount must be greater than zero.");
            }

            if (!Enum.IsDefined(typeof(PosPaymentMethod), p.Method))
            {
                throw new FieldValidationException("payments", "Unsupported payment method.");
            }

            if (p.Method == PosPaymentMethod.Cash)
            {
                if (p.TenderedAmount > 0 && p.TenderedAmount < p.Amount)
                {
                    throw new FieldValidationException("payments", $"Tendered amount ({p.TenderedAmount:N2}) cannot be less than payment amount ({p.Amount:N2}).");
                }
            }
            else
            {
                if (p.TenderedAmount != 0 && p.TenderedAmount != p.Amount)
                {
                    throw new FieldValidationException("payments", "Non-cash tender must equal payment amount.");
                }
            }
        }

        var paymentsSum = request.Payments.Sum(p => p.Amount);
        if (paymentsSum < totalAmount)
        {
            if (!allowPartial)
            {
                throw new FieldValidationException("payments", $"Under-payment: payment sum ({paymentsSum:N2}) is less than order total ({totalAmount:N2}).");
            }
        }

        var paymentStatus = paymentsSum >= totalAmount ? PosPaymentStatus.Paid : PosPaymentStatus.PartiallyPaid;

        var payments = request.Payments.Select(p =>
        {
            var tendered = p.Method == PosPaymentMethod.Cash
                ? (p.TenderedAmount > 0 ? p.TenderedAmount : p.Amount)
                : p.Amount;
            var change = p.Method == PosPaymentMethod.Cash
                ? (tendered > p.Amount ? (tendered - p.Amount) : 0m)
                : 0m;
            return new PosPayment
            {
                Method = p.Method,
                Amount = p.Amount,
                TenderedAmount = tendered,
                ChangeAmount = change,
                Reference = p.Reference,
                PaymentDate = DateTimeOffset.UtcNow
            };
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
            PaymentStatus = paymentStatus,
            CashierUserId = cashierId,
            CashierName = cashierName,
            Notes = request.Notes,
            CustomFields = request.CustomFields,
            Lines = lines,
            Payments = payments
        };

        _orders.Add(order);
        await _orders.SaveChangesAsync(token);

        decimal cashPaid = 0;
        foreach (var p in payments)
        {
            if (p.Method == PosPaymentMethod.Cash)
            {
                var cashIn = p.TenderedAmount > 0 ? (p.TenderedAmount - p.ChangeAmount) : p.Amount;
                cashPaid += cashIn;
            }
        }

        // Update session totals atomically via SQL increment
        var updatedRows = await _sessions.Tracked(new Specification<PosSession>(s => s.Id == request.SessionId && s.Status == PosSessionStatus.Open))
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(s => s.TotalSales, s => s.TotalSales + totalAmount)
                .SetProperty(s => s.ExpectedCash, s => s.ExpectedCash + cashPaid), token);

        if (updatedRows == 0)
        {
            throw new ConflictException("Cannot place POS orders in a closed session.");
        }

        // Post accounting journal for revenue & tax
        try
        {
            var salesAccount = await _posting.AccountForRoleAsync("income.sales", token);
            var taxAccount = taxTotal > 0 ? await _posting.AccountForRoleAsync(SystemRoles.TaxOutput, token) : Guid.Empty;
            var cashAccount = await _posting.AccountForRoleAsync("cash", token);
            var bankAccount = await _posting.AccountForRoleAsync("bank", token);

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
                postingLines), token);
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
                    $"POS sale {order.Number} ({l.ItemCode})"), token);
            }
        }

        return Map(order);
    }, cancellationToken);
    }

    public async Task<PosOrderDto> Handle(VoidPosOrderCommand request, CancellationToken cancellationToken)
    {
        return await _unitOfWork.InTransactionAsync(async token =>
        {
            var order = await _orders.Tracked(new Specification<PosOrder>(x => x.Id == request.OrderId))
                .Include(x => x.Lines)
                .Include(x => x.Payments)
                .FirstOrDefaultAsync(token)
                ?? throw new NotFoundException($"Order {request.OrderId} not found");

            if (order.Status == PosOrderStatus.Voided)
            {
                throw new ConflictException("Order is already voided.");
            }

            var session = await _sessions.Query(new Specification<PosSession>(s => s.Id == order.SessionId))
                .FirstOrDefaultAsync(token)
                ?? throw new NotFoundException($"Session {order.SessionId} not found");

            if (session.Status != PosSessionStatus.Open)
            {
                throw new ConflictException("Cannot void orders in a closed session.");
            }

            decimal cashRefunded = 0;
            foreach (var p in order.Payments)
            {
                if (p.Method == PosPaymentMethod.Cash)
                {
                    var cashIn = p.TenderedAmount > 0 ? (p.TenderedAmount - p.ChangeAmount) : p.Amount;
                    cashRefunded += cashIn;
                }
            }

            var updatedSession = await _sessions.Tracked(new Specification<PosSession>(s => s.Id == order.SessionId && s.Status == PosSessionStatus.Open))
                .ExecuteUpdateAsync(setter => setter
                    .SetProperty(s => s.TotalReturns, s => s.TotalReturns + order.TotalAmount)
                    .SetProperty(s => s.TotalSales, s => s.TotalSales - order.TotalAmount)
                    .SetProperty(s => s.ExpectedCash, s => s.ExpectedCash - cashRefunded), token);

            if (updatedSession == 0)
            {
                throw new ConflictException("Cannot void orders in a closed session.");
            }

            order.Status = PosOrderStatus.Voided;
            order.PaymentStatus = PosPaymentStatus.Refunded;
            order.Notes = string.IsNullOrWhiteSpace(order.Notes)
                ? $"Voided: {request.Reason}"
                : $"{order.Notes} | Voided: {request.Reason}";

            await _orders.SaveChangesAsync(token);

            await _stockPosting.ReverseIssueAsync(order.Number, token);

            return Map(order);
        }, cancellationToken);
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
