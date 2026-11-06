using System.Text.Json.Serialization;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Parties;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Domain.Parties;
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
    string? CashierName = null,
    Guid? ParkedOrderId = null) : ICommand<PosOrderDto>
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

/// <summary>Puts a ticket aside without taking money: no number from the sales sequence, no ledger entry, no stock movement.</summary>
public record ParkPosOrderCommand(
    Guid SessionId,
    Guid? CustomerPartyId,
    string? CustomerName,
    List<PosOrderLineInput> Lines,
    string? Notes = null) : ICommand<PosOrderDto>;

public record DiscardParkedOrderCommand(Guid OrderId) : ICommand<Unit>;

public record GetPosOrderByIdQuery(Guid Id) : IQuery<PosOrderDto>;

/// <summary>A sale found by its receipt number, as typed or scanned at the counter.</summary>
public record GetPosOrderByNumberQuery(string Number) : IQuery<PosOrderDto>;

public record PosOrderLineDto(
    Guid Id,
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate,
    decimal LineTotal,
    decimal ReturnedQuantity = 0,
    decimal ReturnedAmount = 0);

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
    DateTimeOffset CreatedAt,
    decimal ReturnedTotal = 0,
    string? InvoiceNumber = null);

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
    DateTimeOffset CreatedAt,
    decimal ReturnedTotal = 0,
    Guid? InvoiceId = null,
    string? InvoiceNumber = null,
    decimal MoneyLeftToRefund = 0,
    decimal OnAccountLeft = 0);

public class VoidPosOrderValidator : AbstractValidator<VoidPosOrderCommand>
{
    public VoidPosOrderValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("A reason is required to void an order.").MaximumLength(500);
    }
}

internal static class PosLineRules
{
    public static void Lines(InlineValidator<PosOrderLineInput> l)
    {
        l.RuleFor(x => x.Quantity).GreaterThan(0);
        l.RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100);
        l.When(x => x.TaxRate.HasValue, () =>
        {
            l.RuleFor(x => x.TaxRate!.Value).InclusiveBetween(0, 100).WithMessage("Tax rate must be between 0 and 100.");
        });
        l.When(x => !x.ItemId.HasValue, () =>
        {
            l.RuleFor(x => x.ItemCode).NotEmpty();
            l.RuleFor(x => x.UnitPrice).NotNull().WithMessage("A free-text line needs a unit price.");
        });
        l.When(x => x.UnitPrice.HasValue, () =>
        {
            l.RuleFor(x => x.UnitPrice!.Value).GreaterThanOrEqualTo(0);
        });
    }
}

public class CreatePosOrderValidator : AbstractValidator<CreatePosOrderCommand>
{
    public CreatePosOrderValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.RegisterId).NotEmpty();
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one item is required.");
        RuleForEach(x => x.Lines).ChildRules(PosLineRules.Lines);
        RuleFor(x => x.Payments).NotEmpty().WithMessage("At least one payment tender is required.");
        RuleForEach(x => x.Payments).ChildRules(p =>
        {
            p.RuleFor(x => x.Amount).GreaterThan(0);
            p.RuleFor(x => x.Method).IsInEnum();
        });
    }
}

public class ParkPosOrderValidator : AbstractValidator<ParkPosOrderCommand>
{
    public ParkPosOrderValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one item is required.");
        RuleForEach(x => x.Lines).ChildRules(PosLineRules.Lines);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public class OrderHandlers :
    IRequestHandler<CreatePosOrderCommand, PosOrderDto>,
    IRequestHandler<VoidPosOrderCommand, PosOrderDto>,
    IRequestHandler<ParkPosOrderCommand, PosOrderDto>,
    IRequestHandler<DiscardParkedOrderCommand, Unit>,
    IRequestHandler<GetPosOrderByIdQuery, PosOrderDto>,
    IRequestHandler<GetPosOrderByNumberQuery, PosOrderDto>
{
    private readonly IRepository<PosOrder> _orders;
    private readonly IRepository<PosSession> _sessions;
    private readonly IRepository<PosRegister> _registers;
    private readonly INumberingService _numbering;
    private readonly IStockPostingService _stockPosting;
    private readonly IPostingService _posting;
    private readonly IReadRepository<Item> _items;
    private readonly IReadRepository<TaxCode> _taxCodes;
    private readonly IReadRepository<Party> _parties;
    private readonly ISettingsService _settings;
    private readonly ICurrentUser _currentUser;
    private readonly IUserDirectory _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<JournalEntry> _journals;
    private readonly IRepository<Invoice> _invoices;
    private readonly IInvoicingService _invoicing;
    private readonly PosAccounts _accounts;
    private readonly IReadRepository<PosReturn> _returns;
    private const string PosJournalSource = "pos";

    public OrderHandlers(
        IRepository<PosOrder> orders,
        IRepository<PosSession> sessions,
        IRepository<PosRegister> registers,
        INumberingService numbering,
        IStockPostingService stockPosting,
        IPostingService posting,
        IReadRepository<Item> items,
        IReadRepository<TaxCode> taxCodes,
        IReadRepository<Party> parties,
        ISettingsService settings,
        ICurrentUser currentUser,
        IUserDirectory users,
        IUnitOfWork unitOfWork,
        IRepository<JournalEntry> journals,
        IRepository<Invoice> invoices,
        IInvoicingService invoicing,
        PosAccounts accounts,
        IReadRepository<PosReturn> returns)
    {
        _returns = returns;
        _journals = journals;
        _orders = orders;
        _sessions = sessions;
        _registers = registers;
        _numbering = numbering;
        _stockPosting = stockPosting;
        _posting = posting;
        _items = items;
        _taxCodes = taxCodes;
        _parties = parties;
        _settings = settings;
        _currentUser = currentUser;
        _users = users;
        _unitOfWork = unitOfWork;
        _invoices = invoices;
        _invoicing = invoicing;
        _accounts = accounts;
    }

    private sealed record PricedLines(List<PosOrderLine> Lines, decimal SubTotal, decimal TaxTotal, decimal DiscountTotal)
    {
        public decimal Total => SubTotal + TaxTotal;
    }

    /// <summary>The server's own prices, tax and discount limits. Sales and held tickets both go through here, so a resumed ticket is priced again at payment.</summary>
    private async Task<PricedLines> PriceAsync(Guid legalEntityId, IReadOnlyList<PosOrderLineInput> input, CancellationToken ct)
    {
        var maxDiscount = await _settings.GetAsync<decimal?>("pos.maxDiscountPercent", legalEntityId, ct) ?? 100m;
        var cashierLimit = await _settings.GetAsync<decimal?>("pos.cashierDiscountPercent", legalEntityId, ct);
        var canOverridePrice = _currentUser.HasPermission(Permissions.OrdersOverridePrice);
        var limit = cashierLimit is { } c && !_currentUser.HasPermission(Permissions.OrdersDiscountOverride) ? Math.Min(c, maxDiscount) : maxDiscount;

        var itemIds = input.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToList();
        var items = itemIds.Count > 0
            ? await _items.Query(new Specification<Item>(i => itemIds.Contains(i.Id))).ToDictionaryAsync(i => i.Id, ct)
            : new Dictionary<Guid, Item>();

        var taxCodeIds = items.Values.Where(i => i.TaxCodeId.HasValue).Select(i => i.TaxCodeId!.Value).Distinct().ToList();
        var taxCodes = taxCodeIds.Count > 0
            ? await _taxCodes.Query(new Specification<TaxCode>(t => taxCodeIds.Contains(t.Id))).ToDictionaryAsync(t => t.Id, ct)
            : new Dictionary<Guid, TaxCode>();

        decimal subtotal = 0, taxTotal = 0, discountTotal = 0;
        var lines = new List<PosOrderLine>();
        foreach (var l in input)
        {
            if (l.DiscountPercent < 0 || l.DiscountPercent > 100)
            {
                throw new FieldValidationException("discountPercent", "Discount must be between 0 and 100.");
            }
            if (l.DiscountPercent > limit)
            {
                var who = limit < maxDiscount ? " for your role; ask someone who can approve a larger discount" : string.Empty;
                throw new FieldValidationException("discountPercent", $"Discount ({l.DiscountPercent}%) exceeds maximum allowed ({limit}%){who}.");
            }

            decimal unitPrice;
            decimal taxRate;
            var itemCode = l.ItemCode ?? string.Empty;
            var description = l.Description ?? string.Empty;

            if (l.ItemId.HasValue)
            {
                if (!items.TryGetValue(l.ItemId.Value, out var item))
                {
                    throw new NotFoundException($"Item {l.ItemId.Value} not found.");
                }
                if (!item.IsActive)
                {
                    throw new ConflictException($"{item.Name} is inactive and cannot be sold.");
                }

                itemCode = string.IsNullOrWhiteSpace(itemCode) ? item.Sku : itemCode;
                description = string.IsNullOrWhiteSpace(description) ? item.Name : description;

                if (item.SalesPrice is null && !(canOverridePrice && l.UnitPrice.HasValue))
                {
                    throw new FieldValidationException("unitPrice", $"{item.Name} has no sales price.");
                }
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
                    taxRate = canOverridePrice ? l.TaxRate ?? 0m : 0m;
                }
            }
            else
            {
                if (!canOverridePrice)
                {
                    throw new FieldValidationException("unitPrice", "Selling a free-text item at a manual price requires override permission.");
                }
                unitPrice = l.UnitPrice ?? 0m;
                taxRate = l.TaxRate ?? 0m;
            }

            var a = PosMath.Line(l.Quantity, unitPrice, l.DiscountPercent, taxRate);
            subtotal += a.Net;
            taxTotal += a.Tax;
            discountTotal += a.Discount;

            lines.Add(new PosOrderLine
            {
                ItemId = l.ItemId,
                ItemCode = itemCode.Trim(),
                Description = description.Trim(),
                Quantity = l.Quantity,
                UnitPrice = unitPrice,
                DiscountPercent = l.DiscountPercent,
                TaxRate = taxRate,
                LineTotal = a.Total
            });
        }

        return new PricedLines(lines, subtotal, taxTotal, discountTotal);
    }

    private async Task<(string Name, Guid Id)> CashierAsync(Guid? requestedId, string? requestedName, CancellationToken ct)
    {
        // Cashier identity from ICurrentUser; client-supplied cashier is ignored
        var cashierId = (_currentUser.UserId.HasValue && _currentUser.UserId.Value != Guid.Empty)
            ? _currentUser.UserId.Value
            : (requestedId ?? Guid.Empty);
        var profile = cashierId != Guid.Empty ? await _users.GetProfileAsync(cashierId, ct) : null;
        var name = !string.IsNullOrWhiteSpace(profile?.FullName)
            ? profile!.FullName
            : !string.IsNullOrWhiteSpace(_currentUser.Email)
            ? _currentUser.Email!
            : (_currentUser.UserId.HasValue ? _currentUser.UserId.Value.ToString() : (!string.IsNullOrWhiteSpace(requestedName) ? requestedName.Trim() : "POS Cashier"));
        return (name, cashierId);
    }

    /// <summary>A chosen customer must really be one, and the name on the ticket then comes from the customer record.</summary>
    private async Task<(Guid? Id, string Name)> CustomerAsync(Guid? partyId, string? typedName, CancellationToken ct)
    {
        if (partyId is null) return (null, string.IsNullOrWhiteSpace(typedName) ? "Walk-in Customer" : typedName.Trim());
        var party = await _parties.FindAsync(partyId.Value, ct) ?? throw new FieldValidationException("customerPartyId", "Customer not found.");
        CustomerRules.RequireCustomer(party);
        return (party.Id, party.Name);
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

            if (request.RegisterId != session.RegisterId) throw new FieldValidationException("registerId", "The register does not match the session.");
            if (request.LegalEntityId != session.LegalEntityId) throw new FieldValidationException("legalEntityId", "The legal entity does not match the session.");
            var register = await _registers.FindAsync(session.RegisterId, token) ?? throw new NotFoundException($"Register {session.RegisterId} not found");
            if (!register.IsActive) throw new ConflictException($"Register '{register.Name}' is inactive.");

            var (cashierName, cashierId) = await CashierAsync(request.CashierUserId, request.CashierName, token);
            var (customerId, customerName) = await CustomerAsync(request.CustomerPartyId, request.CustomerName, token);
            var allowPartial = await _settings.GetAsync<bool?>("pos.allowPartialPayment", request.LegalEntityId, token) ?? false;
            var priced = await PriceAsync(request.LegalEntityId, request.Lines, token);
            var totalAmount = priced.Total;

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
            if (paymentsSum < totalAmount && !allowPartial)
            {
                throw new FieldValidationException("payments", $"Under-payment: payment sum ({paymentsSum:N2}) is less than order total ({totalAmount:N2}).");
            }

            if (paymentsSum > totalAmount)
            {
                throw new FieldValidationException("payments", $"Over-payment: payment sum ({paymentsSum:N2}) exceeds order total ({totalAmount:N2}). Enter the amount applied and put the cash handed over in the tendered amount.");
            }

            // What stays owing goes on the customer's account: named tenders of that kind, plus any balance a partial payment leaves.
            var onAccount = request.Payments.Where(p => p.Method == PosPaymentMethod.Account).Sum(p => p.Amount) + (totalAmount - paymentsSum);
            if (onAccount > 0)
            {
                if (customerId is null) throw new FieldValidationException("customerPartyId", "Choose a customer to put this sale on account.");
                if (!_currentUser.HasPermission(Permissions.OrdersOnAccount)) throw new ForbiddenException("Selling on account needs the on-account permission.");
            }

            var paymentStatus = onAccount <= 0 ? PosPaymentStatus.Paid : onAccount >= totalAmount ? PosPaymentStatus.OnAccount : PosPaymentStatus.PartiallyPaid;

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

            await EnsureStockAsync(priced.Lines, register, token);

            if (request.ParkedOrderId is { } parkedId)
            {
                var parked = await _orders.Tracked(new Specification<PosOrder>(o => o.Id == parkedId && o.Status == PosOrderStatus.Parked)).FirstOrDefaultAsync(token)
                    ?? throw new ConflictException("That held sale is no longer on hold.");
                _orders.Remove(parked);
            }

            var orderNumber = await _numbering.NextAsync("POS", request.LegalEntityId, null, token);

            var order = new PosOrder
            {
                Number = orderNumber,
                LegalEntityId = request.LegalEntityId,
                RegisterId = request.RegisterId,
                SessionId = request.SessionId,
                CustomerPartyId = customerId,
                CustomerName = customerName,
                OrderDate = DateTimeOffset.UtcNow,
                SubTotal = priced.SubTotal,
                TaxTotal = priced.TaxTotal,
                DiscountTotal = priced.DiscountTotal,
                TotalAmount = totalAmount,
                Status = PosOrderStatus.Completed,
                PaymentStatus = paymentStatus,
                CashierUserId = cashierId,
                CashierName = cashierName,
                Notes = request.Notes,
                CustomFields = request.CustomFields,
                Lines = priced.Lines,
                Payments = payments
            };

            _orders.Add(order);
            await _orders.SaveChangesAsync(token);

            var cashPaid = payments.Where(p => p.Method == PosPaymentMethod.Cash).Sum(p => p.Amount);

            // Update session totals atomically via SQL increment
            var updatedRows = await _sessions.Tracked(new Specification<PosSession>(s => s.Id == request.SessionId && s.Status == PosSessionStatus.Open))
                .ExecuteUpdateAsync(setter => setter
                    .SetProperty(s => s.TotalSales, s => s.TotalSales + totalAmount)
                    .SetProperty(s => s.ExpectedCash, s => s.ExpectedCash + cashPaid), token);

            if (updatedRows == 0)
            {
                throw new ConflictException("Cannot place POS orders in a closed session.");
            }

            // Post the sale to the ledger in the same transaction: a failure here rolls the whole order back.
            var sales = await _accounts.SalesAsync(token);
            var income = await _accounts.IncomeAccountsAsync(priced.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value), token);
            var postingLines = new List<PostingLine>();
            foreach (var p in payments.Where(p => p.Method != PosPaymentMethod.Account))
            {
                postingLines.Add(new PostingLine(await _accounts.TenderAsync(register, p.Method, token), p.Amount, 0, $"POS payment ({p.Method}) - {order.Number}"));
            }
            foreach (var g in priced.Lines.GroupBy(l => l.ItemId is { } id && income.TryGetValue(id, out var acct) ? acct : sales))
            {
                postingLines.Add(new PostingLine(g.Key, 0, g.Sum(l => PosMath.Line(l).Net), $"POS sales revenue - {order.Number}"));
            }
            if (priced.TaxTotal > 0)
            {
                postingLines.Add(new PostingLine(await _accounts.TaxAsync(token), 0, priced.TaxTotal, $"POS tax output - {order.Number}"));
            }

            if (onAccount > 0)
            {
                // Receivables live on invoices (statements, ageing and collections all read them), so the balance becomes a real invoice.
                // That invoice credits sales; this entry takes the same amount back so the sale counts once.
                var invoice = await _invoicing.BuildAsync(new InvoiceDraft(
                    order.LegalEntityId, customerId!.Value, DateOnly.FromDateTime(order.OrderDate.UtcDateTime), null,
                    [new InvoiceLineDraft($"Counter sale {order.Number}, on account", 1, onAccount, AccountId: sales)],
                    SourceType: PosJournalSource, SourceId: order.Id, Notes: $"Counter sale {order.Number}"), token);
                await _invoicing.IssueAsync(invoice, token);
                order.InvoiceId = invoice.Id;
                order.InvoiceNumber = invoice.Number;
                postingLines.Add(new PostingLine(sales, onAccount, 0, $"On account, invoice {invoice.Number} - {order.Number}"));
            }

            var journalLines = PosMath.Consolidate(postingLines);
            if (journalLines.Count > 0)
            {
                await _posting.PostAsync(new PostingRequest(
                    order.LegalEntityId,
                    DateOnly.FromDateTime(order.OrderDate.UtcDateTime),
                    PosJournalSource,
                    order.Id,
                    $"POS Order {order.Number}",
                    order.Number,
                    journalLines), token);
            }
            await _orders.SaveChangesAsync(token);

            foreach (var l in priced.Lines)
            {
                if (l.ItemId.HasValue && l.Quantity > 0)
                {
                    await _stockPosting.IssueStockAsync(new StockIssueRequest(
                        order.LegalEntityId,
                        l.ItemId.Value,
                        l.Quantity,
                        DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow),
                        order.Number,
                        $"POS sale {order.Number} ({l.ItemCode})",
                        register.WarehouseId), token);
                }
            }

            return Map(order);
        }, cancellationToken);
    }

    /// <summary>With "allow negative stock" off, a sale cannot take a tracked item below zero.</summary>
    private async Task EnsureStockAsync(IReadOnlyList<PosOrderLine> lines, PosRegister register, CancellationToken ct)
    {
        var allowNegative = await _settings.GetAsync<bool?>("pos.allowNegativeStock", register.LegalEntityId, ct) ?? true;
        if (allowNegative) return;
        var wanted = lines.Where(l => l.ItemId.HasValue).GroupBy(l => l.ItemId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        var onHand = await _stockPosting.GetOnHandAsync(wanted.Keys, register.WarehouseId, register.LegalEntityId, ct);
        foreach (var (itemId, qty) in onHand)
        {
            if (wanted[itemId] > qty)
            {
                var line = lines.First(l => l.ItemId == itemId);
                throw new ConflictException($"Only {qty:0.##} of {line.Description} in stock.");
            }
        }
    }

    public async Task<PosOrderDto> Handle(ParkPosOrderCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessions.Query(new Specification<PosSession>(s => s.Id == request.SessionId)).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");
        if (session.Status != PosSessionStatus.Open) throw new ConflictException("Open a shift to hold a sale.");

        var (cashierName, cashierId) = await CashierAsync(null, null, cancellationToken);
        var (customerId, customerName) = await CustomerAsync(request.CustomerPartyId, request.CustomerName, cancellationToken);
        var priced = await PriceAsync(session.LegalEntityId, request.Lines, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var order = new PosOrder
        {
            Number = $"HOLD-{now:yyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
            LegalEntityId = session.LegalEntityId,
            RegisterId = session.RegisterId,
            SessionId = session.Id,
            CustomerPartyId = customerId,
            CustomerName = customerName,
            OrderDate = now,
            SubTotal = priced.SubTotal,
            TaxTotal = priced.TaxTotal,
            DiscountTotal = priced.DiscountTotal,
            TotalAmount = priced.Total,
            Status = PosOrderStatus.Parked,
            PaymentStatus = PosPaymentStatus.Unpaid,
            CashierUserId = cashierId,
            CashierName = cashierName,
            Notes = request.Notes,
            Lines = priced.Lines,
        };
        _orders.Add(order);
        await _orders.SaveChangesAsync(cancellationToken);
        return Map(order);
    }

    public async Task<Unit> Handle(DiscardParkedOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orders.Tracked(new Specification<PosOrder>(o => o.Id == request.OrderId)).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Order {request.OrderId} not found");
        if (order.Status != PosOrderStatus.Parked) throw new ConflictException("Only a held sale can be discarded.");
        _orders.Remove(order);
        await _orders.SaveChangesAsync(cancellationToken);
        return Unit.Value;
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
            if (order.Status != PosOrderStatus.Completed)
            {
                throw new ConflictException("Only a completed order can be voided.");
            }
            if (order.ReturnedTotal > 0)
            {
                throw new ConflictException("Part of this sale has been returned, so it can no longer be voided. Return the rest instead.");
            }

            var session = await _sessions.Query(new Specification<PosSession>(s => s.Id == order.SessionId))
                .FirstOrDefaultAsync(token)
                ?? throw new NotFoundException($"Session {order.SessionId} not found");

            if (session.Status != PosSessionStatus.Open)
            {
                throw new ConflictException("Cannot void orders in a closed session.");
            }

            var cashRefunded = order.Payments.Where(p => p.Method == PosPaymentMethod.Cash).Sum(p => p.Amount);

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

            if (order.InvoiceId is { } invoiceId)
            {
                var invoice = await _invoices.Tracked(new Specification<Invoice>(i => i.Id == invoiceId)).FirstOrDefaultAsync(token)
                    ?? throw new NotFoundException($"Invoice {order.InvoiceNumber} not found");
                await _invoicing.VoidAsync(invoice, $"Void POS order {order.Number}", token); // refuses once the customer has paid some of it
            }

            await _orders.SaveChangesAsync(token);

            await _stockPosting.ReverseIssueAsync(order.Number, token);

            var journal = await _journals.Tracked(new Specification<JournalEntry>(j => j.SourceType == PosJournalSource && j.SourceId == order.Id && j.Status == JournalStatus.Posted).Include(j => j.Lines))
                .FirstOrDefaultAsync(token);
            if (journal is not null)
            {
                await _posting.ReverseTodayAsync(journal, $"Void POS order {order.Number}", token);
                await _orders.SaveChangesAsync(token);
            }

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

        return await WithRefundableAsync(order, cancellationToken);
    }

    public async Task<PosOrderDto> Handle(GetPosOrderByNumberQuery request, CancellationToken cancellationToken)
    {
        var number = request.Number.Trim();
        var order = await _orders.Query(new Specification<PosOrder>(x => x.Number == number))
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"No sale with receipt number {number}.");

        return await WithRefundableAsync(order, cancellationToken);
    }

    /// <summary>What can still go back in money and what is still on account, so a return can be split the way the sale was paid.</summary>
    private async Task<PosOrderDto> WithRefundableAsync(PosOrder order, CancellationToken ct)
    {
        var prior = await _returns.Query(new Specification<PosReturn>(r => r.OriginalOrderId == order.Id)).SelectMany(r => r.Refunds).Select(f => new { f.Method, f.Amount }).ToListAsync(ct);
        var paid = order.Payments.Where(p => p.Method != PosPaymentMethod.Account).Sum(p => p.Amount);
        return Map(order) with
        {
            MoneyLeftToRefund = paid - prior.Where(f => f.Method != PosPaymentMethod.Account).Sum(f => f.Amount),
            OnAccountLeft = order.TotalAmount - paid - prior.Where(f => f.Method == PosPaymentMethod.Account).Sum(f => f.Amount),
        };
    }

    internal static PosOrderDto Map(PosOrder o) => new(
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
            l.LineTotal,
            l.ReturnedQuantity,
            l.ReturnedAmount)).ToList(),
        o.Payments.Select(p => new PosPaymentDto(
            p.Id,
            p.Method,
            p.Amount,
            p.TenderedAmount,
            p.ChangeAmount,
            p.Reference,
            p.PaymentDate)).ToList(),
        o.CreatedAt,
        o.ReturnedTotal,
        o.InvoiceId,
        o.InvoiceNumber);
}
