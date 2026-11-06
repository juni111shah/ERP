using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using ERP.Modules.Pos.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Features;

public record PosReturnLineInput(Guid OrderLineId, decimal Quantity, bool Restock = true);

public record PosRefundInput(PosPaymentMethod Method, decimal Amount, string? Reference = null);

/// <summary>SessionId is the shift giving the money back, which need not be the shift that made the sale.</summary>
public record CreatePosReturnCommand(
    Guid OrderId,
    Guid SessionId,
    string Reason,
    List<PosReturnLineInput> Lines,
    List<PosRefundInput> Refunds) : ICommand<PosReturnDto>;

public record GetPosReturnByIdQuery(Guid Id) : IQuery<PosReturnDto>;

public record PosReturnLineDto(
    Guid Id,
    Guid OrderLineId,
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate,
    decimal NetAmount,
    decimal TaxAmount,
    decimal LineTotal,
    bool Restock);

public record PosReturnRefundDto(Guid Id, PosPaymentMethod Method, decimal Amount, string? Reference);

public record PosReturnSummaryDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid OriginalOrderId,
    string OriginalNumber,
    Guid RegisterId,
    Guid SessionId,
    string CustomerName,
    DateTimeOffset ReturnDate,
    decimal SubTotal,
    decimal TaxTotal,
    decimal TotalAmount,
    string Reason,
    string CashierName,
    string? CreditNoteNumber);

public record PosReturnDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid OriginalOrderId,
    string OriginalNumber,
    Guid RegisterId,
    Guid SessionId,
    Guid? CustomerPartyId,
    string CustomerName,
    DateTimeOffset ReturnDate,
    decimal SubTotal,
    decimal TaxTotal,
    decimal TotalAmount,
    string Reason,
    string CashierName,
    Guid? CreditNoteId,
    string? CreditNoteNumber,
    List<PosReturnLineDto> Lines,
    List<PosReturnRefundDto> Refunds);

public class CreatePosReturnValidator : AbstractValidator<CreatePosReturnCommand>
{
    public CreatePosReturnValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Say why the goods came back.").MaximumLength(500);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Choose at least one item to return.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.OrderLineId).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0);
        });
        RuleFor(x => x.Refunds).NotEmpty().WithMessage("Say how the money goes back.");
        RuleForEach(x => x.Refunds).ChildRules(r =>
        {
            r.RuleFor(x => x.Amount).GreaterThan(0);
            r.RuleFor(x => x.Method).IsInEnum();
            r.RuleFor(x => x.Reference).MaximumLength(100);
        });
    }
}

public class ReturnHandlers(
    IRepository<PosReturn> returns,
    IRepository<PosOrder> orders,
    IRepository<PosSession> sessions,
    IRepository<PosRegister> registers,
    INumberingService numbering,
    IStockPostingService stock,
    IPostingService posting,
    IInvoicingService invoicing,
    IRepository<Invoice> invoices,
    IRepository<CreditApplication> creditApplications,
    ISettingsService settings,
    ICurrentUser user,
    IUserDirectory users,
    IUnitOfWork uow,
    PosAccounts accounts) :
    IRequestHandler<CreatePosReturnCommand, PosReturnDto>,
    IRequestHandler<GetPosReturnByIdQuery, PosReturnDto>
{
    private const string JournalSource = "pos-return";

    public Task<PosReturnDto> Handle(CreatePosReturnCommand request, CancellationToken cancellationToken) => uow.InTransactionAsync(async token =>
    {
        var caller = user.UserId is { } id && id != Guid.Empty ? id : throw new ForbiddenException();
        var order = await orders.Tracked(new Specification<PosOrder>(o => o.Id == request.OrderId))
            .Include(o => o.Lines).Include(o => o.Payments).FirstOrDefaultAsync(token)
            ?? throw new NotFoundException($"Order {request.OrderId} not found");
        if (order.Status != PosOrderStatus.Completed) throw new ConflictException("Only a completed sale can be returned.");

        var session = await sessions.Query(new Specification<PosSession>(s => s.Id == request.SessionId)).FirstOrDefaultAsync(token)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");
        if (session.Status != PosSessionStatus.Open) throw new ConflictException("Open a shift to give money back.");
        if (session.LegalEntityId != order.LegalEntityId) throw new FieldValidationException("sessionId", "This shift belongs to another legal entity.");
        var register = await registers.FindAsync(session.RegisterId, token) ?? throw new NotFoundException($"Register {session.RegisterId} not found");

        // What earlier returns already gave back per line, so the last unit takes what is left and rounding can never over-refund.
        var earlier = await returns.Query(new Specification<PosReturn>(r => r.OriginalOrderId == order.Id)).SelectMany(r => r.Lines)
            .Select(l => new { l.OrderLineId, l.NetAmount, l.TaxAmount }).ToListAsync(token);

        var lines = new List<PosReturnLine>();
        foreach (var input in request.Lines.GroupBy(l => l.OrderLineId))
        {
            var line = order.Lines.FirstOrDefault(l => l.Id == input.Key) ?? throw new FieldValidationException("lines", "That line is not on this sale.");
            var qty = input.Sum(l => l.Quantity);
            var left = line.Quantity - line.ReturnedQuantity;
            if (qty > left) throw new ConflictException($"Only {left:0.##} of {line.Description} can still be returned.");

            // Worked out as a running share of what the line charged: each return takes its share less what earlier ones took,
            // so the refunds never exceed what was paid, no part goes negative, and the last unit lands exactly on the total.
            var fullNet = PosMath.Line(line).Net;
            var fullTax = line.LineTotal - fullNet;
            var soFar = line.ReturnedQuantity + qty;
            var net = Money.Round(fullNet * soFar / line.Quantity) - earlier.Where(e => e.OrderLineId == line.Id).Sum(e => e.NetAmount);
            var tax = Money.Round(fullTax * soFar / line.Quantity) - earlier.Where(e => e.OrderLineId == line.Id).Sum(e => e.TaxAmount);

            line.ReturnedQuantity += qty;
            line.ReturnedAmount += net + tax;
            lines.Add(new PosReturnLine
            {
                OrderLineId = line.Id,
                ItemId = line.ItemId,
                ItemCode = line.ItemCode,
                Description = line.Description,
                Quantity = qty,
                UnitPrice = line.UnitPrice,
                DiscountPercent = line.DiscountPercent,
                TaxRate = line.TaxRate,
                NetAmount = net,
                TaxAmount = tax,
                LineTotal = net + tax,
                Restock = input.All(l => l.Restock),
            });
        }

        var subTotal = lines.Sum(l => l.NetAmount);
        var taxTotal = lines.Sum(l => l.TaxAmount);
        var total = subTotal + taxTotal;
        var refunded = request.Refunds.Sum(r => r.Amount);
        if (refunded != total) throw new FieldValidationException("refunds", $"The refund ({refunded:N2}) must equal the return total ({total:N2}).");

        var toAccount = request.Refunds.Where(r => r.Method == PosPaymentMethod.Account).Sum(r => r.Amount);
        if (toAccount > 0 && order.InvoiceId is null) throw new FieldValidationException("refunds", "This sale was not on account, so the money cannot go back to an account.");

        // Money only goes back the way it came: nobody collects cash for goods they still owe for.
        var priorRefunds = await returns.Query(new Specification<PosReturn>(r => r.OriginalOrderId == order.Id)).SelectMany(r => r.Refunds).Select(f => new { f.Method, f.Amount }).ToListAsync(token);
        var paid = order.Payments.Where(p => p.Method != PosPaymentMethod.Account).Sum(p => p.Amount);
        var paidLeft = paid - priorRefunds.Where(f => f.Method != PosPaymentMethod.Account).Sum(f => f.Amount);
        var moneyBack = refunded - toAccount;
        if (moneyBack > paidLeft) throw new ConflictException($"Only {paidLeft:N2} of this sale was paid and is left to refund. The rest goes back to the customer's account.");
        var owedLeft = order.TotalAmount - paid - priorRefunds.Where(f => f.Method == PosPaymentMethod.Account).Sum(f => f.Amount);
        if (toAccount > owedLeft) throw new ConflictException($"Only {owedLeft:N2} of this sale is still on account.");
        var cash = request.Refunds.Where(r => r.Method == PosPaymentMethod.Cash).Sum(r => r.Amount);

        var allowNegative = await settings.GetAsync<bool?>("pos.allowNegativeDrawer", order.LegalEntityId, token) ?? false;
        var rows = await sessions.Tracked(new Specification<PosSession>(s => s.Id == session.Id && s.Status == PosSessionStatus.Open && (allowNegative || s.ExpectedCash >= cash)))
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.TotalReturns, s => s.TotalReturns + total)
                .SetProperty(s => s.ExpectedCash, s => s.ExpectedCash - cash), token);
        if (rows == 0)
        {
            var stillOpen = await sessions.AnyAsync(s => s.Id == session.Id && s.Status == PosSessionStatus.Open, token);
            throw new ConflictException(stillOpen ? $"The drawer does not hold {cash:N2} in cash. Refund another way or add cash first." : "Open a shift to give money back.");
        }

        var profile = await users.GetProfileAsync(caller, token);
        var now = DateTimeOffset.UtcNow;
        var ret = new PosReturn
        {
            Number = await numbering.NextAsync("POSR", order.LegalEntityId, null, token),
            LegalEntityId = order.LegalEntityId,
            OriginalOrderId = order.Id,
            OriginalNumber = order.Number,
            RegisterId = session.RegisterId,
            SessionId = session.Id,
            CustomerPartyId = order.CustomerPartyId,
            CustomerName = order.CustomerName,
            ReturnDate = now,
            SubTotal = subTotal,
            TaxTotal = taxTotal,
            TotalAmount = total,
            Reason = request.Reason.Trim(),
            CashierUserId = caller,
            CashierName = !string.IsNullOrWhiteSpace(profile?.FullName) ? profile!.FullName : user.Email ?? caller.ToString(),
            Lines = lines,
            Refunds = request.Refunds.Select(r => new PosReturnRefund { Method = r.Method, Amount = r.Amount, Reference = r.Reference }).ToList(),
        };

        order.ReturnedTotal += total;
        order.PaymentStatus = order.Lines.All(l => l.ReturnedQuantity >= l.Quantity) ? PosPaymentStatus.Refunded : PosPaymentStatus.PartiallyRefunded;

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var sales = await accounts.SalesAsync(token);
        var income = await accounts.IncomeAccountsAsync(lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value), token);
        var memo = $"POS return {ret.Number} of {order.Number}";
        var posting_ = new List<PostingLine>();
        foreach (var g in lines.GroupBy(l => l.ItemId is { } i && income.TryGetValue(i, out var a) ? a : sales))
            posting_.Add(new PostingLine(g.Key, g.Sum(l => l.NetAmount), 0, memo));
        if (taxTotal > 0) posting_.Add(new PostingLine(await accounts.TaxAsync(token), taxTotal, 0, memo));
        foreach (var r in request.Refunds.Where(r => r.Method != PosPaymentMethod.Account))
            posting_.Add(new PostingLine(await accounts.TenderAsync(register, r.Method, token), 0, r.Amount, memo));

        if (toAccount > 0)
        {
            // The mirror of an on-account sale: a credit note lowers what the customer owes and debits sales; this entry takes that debit back.
            var note = await invoicing.BuildAsync(new InvoiceDraft(
                order.LegalEntityId, order.CustomerPartyId!.Value, today, null,
                [new InvoiceLineDraft($"Counter return {ret.Number} of {order.Number}", 1, toAccount, AccountId: sales)],
                IsCreditNote: true, SourceType: "pos", SourceId: ret.Id, Notes: memo, CreditNoteForId: order.InvoiceId), token);
            await invoicing.IssueAsync(note, token);
            // An invoice still owing is reduced straight away; one already paid leaves the credit with the customer.
            var original = await invoices.Tracked(new Specification<Invoice>(i => i.Id == order.InvoiceId)).FirstOrDefaultAsync(token);
            if (original?.Status is DocumentStatus.Issued or DocumentStatus.PartlyPaid)
            {
                var use = Math.Min(note.Total, original.Total - original.Paid);
                if (use > 0)
                {
                    note.Paid += use;
                    original.Paid += use;
                    SettlementStatus.Refresh(note);
                    SettlementStatus.Refresh(original);
                    CreditApplications.Record(creditApplications, note, original, use, note.InvoiceDate, caller);
                }
            }
            ret.CreditNoteId = note.Id;
            ret.CreditNoteNumber = note.Number;
            posting_.Add(new PostingLine(sales, 0, toAccount, memo));
        }

        var journalLines = PosMath.Consolidate(posting_);
        if (journalLines.Count > 0)
            await posting.PostAsync(new PostingRequest(ret.LegalEntityId, today, JournalSource, ret.Id, memo, ret.Number, journalLines), token);

        returns.Add(ret);
        await returns.SaveChangesAsync(token);

        foreach (var l in lines.Where(l => l.Restock && l.ItemId.HasValue))
            await stock.ReturnIssuedStockAsync(order.Number, l.ItemId!.Value, l.Quantity, ret.Number, today, token);

        return Map(ret);
    }, cancellationToken);

    public async Task<PosReturnDto> Handle(GetPosReturnByIdQuery request, CancellationToken cancellationToken)
    {
        var ret = await returns.Query(new Specification<PosReturn>(r => r.Id == request.Id).Include(r => r.Lines).Include(r => r.Refunds)).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Return {request.Id} not found");
        return Map(ret);
    }

    private static PosReturnDto Map(PosReturn r) => new(
        r.Id, r.Number, r.LegalEntityId, r.OriginalOrderId, r.OriginalNumber, r.RegisterId, r.SessionId, r.CustomerPartyId, r.CustomerName, r.ReturnDate,
        r.SubTotal, r.TaxTotal, r.TotalAmount, r.Reason, r.CashierName, r.CreditNoteId, r.CreditNoteNumber,
        r.Lines.Select(l => new PosReturnLineDto(l.Id, l.OrderLineId, l.ItemId, l.ItemCode, l.Description, l.Quantity, l.UnitPrice, l.DiscountPercent, l.TaxRate, l.NetAmount, l.TaxAmount, l.LineTotal, l.Restock)).ToList(),
        r.Refunds.Select(f => new PosReturnRefundDto(f.Id, f.Method, f.Amount, f.Reference)).ToList());
}
