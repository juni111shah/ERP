using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

internal sealed class CreatePosOrderHandler(
    IPosOrderRepository orders,
    IPosSessionRepository sessions,
    IRepository<PosRegister> registers,
    INumberingService numbering,
    IStockPostingService stockPosting,
    IPostingService posting,
    ISettingsService settings,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IInvoicingService invoicing,
    PosAccounts accounts,
    PosOrderPricing pricing,
    PosOrderParties people) : ICommandHandler<CreatePosOrderCommand, PosOrderDto>
{
    public async Task<PosOrderDto> Handle(CreatePosOrderCommand request, CancellationToken cancellationToken)
    {
        return await unitOfWork.InTransactionAsync(async token =>
        {
            // 1. The sale goes into an open shift, on its own active register and legal entity.
            var session = await sessions.FindAsync(request.SessionId, token)
                ?? throw new NotFoundException($"Session {request.SessionId} not found");

            if (session.Status != PosSessionStatus.Open)
            {
                throw new ConflictException("Cannot place POS orders in a closed session.");
            }

            if (request.RegisterId != session.RegisterId) throw new FieldValidationException("registerId", "The register does not match the session.");
            if (request.LegalEntityId != session.LegalEntityId) throw new FieldValidationException("legalEntityId", "The legal entity does not match the session.");
            var register = await registers.FindAsync(session.RegisterId, token) ?? throw new NotFoundException($"Register {session.RegisterId} not found");
            if (!register.IsActive) throw new ConflictException($"Register '{register.Name}' is inactive.");

            // 2. Price the lines on the server.
            var (cashierName, cashierId) = await people.CashierAsync(request.CashierUserId, request.CashierName, token);
            var (customerId, customerName) = await people.CustomerAsync(request.CustomerPartyId, request.CustomerName, token);
            var allowPartial = await settings.GetAsync<bool?>("pos.allowPartialPayment", request.LegalEntityId, token) ?? false;
            var priced = await pricing.ApplyPromoAsync(request.LegalEntityId, await pricing.PriceAsync(request.LegalEntityId, customerId, request.Lines, token), request.PromoCode, token);
            var totalAmount = priced.Total;

            // 3. Payments validation
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

            // 4. What stays owing goes on the customer's account: named tenders of that kind, plus any balance a partial payment leaves.
            var onAccount = request.Payments.Where(p => p.Method == PosPaymentMethod.Account).Sum(p => p.Amount) + (totalAmount - paymentsSum);
            if (onAccount > 0)
            {
                if (customerId is null) throw new FieldValidationException("customerPartyId", "Choose a customer to put this sale on account.");
                if (!currentUser.HasPermission(Permissions.OrdersOnAccount)) throw new ForbiddenException("Selling on account needs the on-account permission.");
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

            // 5. A resumed held ticket gives way to the sale.
            if (request.ParkedOrderId is { } parkedId)
            {
                var parked = await orders.FindParkedForUpdateAsync(parkedId, token)
                    ?? throw new ConflictException("That held sale is no longer on hold.");
                orders.Remove(parked);
            }

            // 6. Save the sale.
            var orderNumber = await numbering.NextAsync("POS", request.LegalEntityId, null, token);

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
                PromoCode = priced.Promo?.Code,
                PromotionId = priced.Promo?.DiscountId,
                PromoDiscount = priced.Promo?.Amount ?? 0m,
                Lines = priced.Lines,
                Payments = payments
            };

            orders.Add(order);
            await orders.SaveChangesAsync(token);

            // Counted with the sale, so a sale that fails later leaves the use unspent and two tills cannot both take the last one.
            if (priced.Promo is { } promo) await pricing.PriceResolver!.RedeemDiscountAsync(promo.DiscountId, token);

            var cashPaid = payments.Where(p => p.Method == PosPaymentMethod.Cash).Sum(p => p.Amount);

            // 7. Update session totals atomically via SQL increment
            var updatedRows = await sessions.AddSaleAsync(request.SessionId, totalAmount, cashPaid, token);

            if (updatedRows == 0)
            {
                throw new ConflictException("Cannot place POS orders in a closed session.");
            }

            // 8. Post the sale to the ledger in the same transaction: a failure here rolls the whole order back.
            var sales = await accounts.SalesAsync(token);
            var income = await accounts.IncomeAccountsAsync(priced.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value), token);
            var postingLines = new List<PostingLine>();
            foreach (var p in payments.Where(p => p.Method != PosPaymentMethod.Account))
            {
                postingLines.Add(new PostingLine(await accounts.TenderAsync(register, p.Method, token), p.Amount, 0, $"POS payment ({p.Method}) - {order.Number}"));
            }
            foreach (var g in priced.Lines.GroupBy(l => l.ItemId is { } id && income.TryGetValue(id, out var acct) ? acct : sales))
            {
                postingLines.Add(new PostingLine(g.Key, 0, g.Sum(l => PosMath.Line(l).Net), $"POS sales revenue - {order.Number}"));
            }
            if (priced.TaxTotal > 0)
            {
                postingLines.Add(new PostingLine(await accounts.TaxAsync(token), 0, priced.TaxTotal, $"POS tax output - {order.Number}"));
            }

            if (onAccount > 0)
            {
                // Receivables live on invoices (statements, ageing and collections all read them), so the balance becomes a real invoice.
                // That invoice credits sales; this entry takes the same amount back so the sale counts once.
                var invoice = await invoicing.BuildAsync(new InvoiceDraft(
                    order.LegalEntityId, customerId!.Value, DateOnly.FromDateTime(order.OrderDate.UtcDateTime), null,
                    [new InvoiceLineDraft($"Counter sale {order.Number}, on account", 1, onAccount, AccountId: sales)],
                    SourceType: PosOrderJournal.Source, SourceId: order.Id, Notes: $"Counter sale {order.Number}"), token);
                await invoicing.IssueAsync(invoice, token);
                order.InvoiceId = invoice.Id;
                order.InvoiceNumber = invoice.Number;
                postingLines.Add(new PostingLine(sales, onAccount, 0, $"On account, invoice {invoice.Number} - {order.Number}"));
            }

            var journalLines = PosMath.Consolidate(postingLines);
            if (journalLines.Count > 0)
            {
                await posting.PostAsync(new PostingRequest(
                    order.LegalEntityId,
                    DateOnly.FromDateTime(order.OrderDate.UtcDateTime),
                    PosOrderJournal.Source,
                    order.Id,
                    $"POS Order {order.Number}",
                    order.Number,
                    journalLines), token);
            }
            await orders.SaveChangesAsync(token);

            // 9. Issue the stock.
            foreach (var l in priced.Lines)
            {
                if (l.ItemId.HasValue && l.Quantity > 0)
                {
                    await stockPosting.IssueStockAsync(new StockIssueRequest(
                        order.LegalEntityId,
                        l.ItemId.Value,
                        l.Quantity,
                        DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow),
                        order.Number,
                        $"POS sale {order.Number} ({l.ItemCode})",
                        register.WarehouseId), token);
                }
            }

            return PosOrderDto.From(order);
        }, cancellationToken);
    }

    /// <summary>With "allow negative stock" off, a sale cannot take a tracked item below zero.</summary>
    private async Task EnsureStockAsync(IReadOnlyList<PosOrderLine> lines, PosRegister register, CancellationToken ct)
    {
        var allowNegative = await settings.GetAsync<bool?>("pos.allowNegativeStock", register.LegalEntityId, ct) ?? true;
        if (allowNegative) return;
        var wanted = lines.Where(l => l.ItemId.HasValue).GroupBy(l => l.ItemId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        var onHand = await stockPosting.GetOnHandAsync(wanted.Keys, register.WarehouseId, register.LegalEntityId, ct);
        foreach (var (itemId, qty) in onHand)
        {
            if (wanted[itemId] > qty)
            {
                var line = lines.First(l => l.ItemId == itemId);
                throw new ConflictException($"Only {qty:0.##} of {line.Description} in stock.");
            }
        }
    }
}
