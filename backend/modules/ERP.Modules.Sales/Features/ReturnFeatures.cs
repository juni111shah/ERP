using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Platform;
using ERP.Modules.Sales.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Sales.Features;

/// <summary>One of OrderLineId or InvoiceLineId, matching what the return is raised against.</summary>
public sealed record SalesReturnLineInput(Guid? OrderLineId, Guid? InvoiceLineId, decimal Quantity, bool Restock = true);
/// <summary>Exactly one of SalesOrderId or InvoiceId.</summary>
public sealed record SalesReturnInput(Guid? SalesOrderId, Guid? InvoiceId, DateOnly ReturnDate, string Reason, List<SalesReturnLineInput> Lines, string? Notes = null);

public sealed record SalesReturnLineDto(Guid Id, Guid? OrderLineId, Guid? InvoiceLineId, Guid? ItemId, string ItemCode, string Description, decimal Quantity, bool Restock);
public sealed record SalesReturnSummaryDto(
    Guid Id, string Number, Guid LegalEntityId, Guid? SalesOrderId, string? OrderNumber, Guid? InvoiceId, string? InvoiceNumber, Guid CustomerPartyId, string CustomerName,
    DateOnly ReturnDate, SalesReturnStatus Status, Guid? CreditNoteId, string? CreditNoteNumber, decimal TotalAmount, int LineCount, DateTime CreatedAt);
public sealed record SalesReturnDto(
    Guid Id, string Number, Guid LegalEntityId, Guid? SalesOrderId, string? OrderNumber, Guid? InvoiceId, string? InvoiceNumber, Guid CustomerPartyId, string CustomerName,
    DateOnly ReturnDate, SalesReturnStatus Status, string Reason, string? Notes, Guid? CreditNoteId, string? CreditNoteNumber, decimal SubTotal, decimal TaxTotal, decimal TotalAmount,
    IReadOnlyList<SalesReturnLineDto> Lines, string? VoidReason, DateTime? VoidedAt, DateTime CreatedAt);

public sealed record GetSalesReturn(Guid Id) : IQuery<SalesReturnDto>, IRequirePermission { public string RequiredPermission => P.ReturnsRead; }
public sealed record CreateSalesReturn(SalesReturnInput Input) : ICommand<SalesReturnDto>, IRequirePermission { public string RequiredPermission => P.ReturnsManage; }
public sealed record DeleteSalesReturn(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.ReturnsManage; }
/// <summary>Restocks at the cost the goods left at (Dr inventory, Cr cost of sales) and, when the goods were invoiced, issues the credit note. It stays unapplied until someone applies it, as any credit note does.</summary>
public sealed record PostSalesReturn(Guid Id) : ICommand<SalesReturnDto>, IRequirePermission { public string RequiredPermission => P.ReturnsManage; }
/// <summary>Reverses the stock receipt and its journal and voids the credit note. Refused once the credit note is applied or the stock is used.</summary>
public sealed record VoidSalesReturn(Guid Id, string? Reason) : ICommand<SalesReturnDto>, IRequirePermission { public string RequiredPermission => P.ReturnsManage; }

public sealed class CreateSalesReturnValidator : AbstractValidator<CreateSalesReturn>
{
    public CreateSalesReturnValidator()
    {
        RuleFor(x => x.Input).Must(i => (i.SalesOrderId is null) != (i.InvoiceId is null)).WithName("salesOrderId").WithMessage("Raise the return against either a sales order or an invoice.");
        RuleFor(x => x.Input.ReturnDate).MustBePlausibleDate();
        RuleFor(x => x.Input.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Input.Notes).MaximumLength(1000);
        RuleFor(x => x.Input.Lines).NotEmpty().WithMessage("A return needs at least one line.");
        RuleFor(x => x.Input.Lines).Must(l => l is null || l.Select(x => (x.OrderLineId, x.InvoiceLineId)).Distinct().Count() == l.Count).WithMessage("A line can appear only once.");
        RuleForEach(x => x.Input.Lines).ChildRules(l => l.RuleFor(x => x.Quantity).GreaterThan(0).MustHaveAtMostDecimals(4));
    }
}

public sealed class VoidSalesReturnValidator : AbstractValidator<VoidSalesReturn>
{
    public VoidSalesReturnValidator() => RuleFor(x => x.Reason).ValidVoidReason();
}

internal sealed class SalesReturnHandlers(
    IRepository<SalesReturn> returns,
    IRepository<SalesOrder> orders,
    IReadRepository<Invoice> invoices,
    IStockPostingService stock,
    ILegalEntityGuard guard,
    INumberingService numbers,
    ISender sender,
    IRepository<AuditEvent> audit,
    ICurrentUser user,
    IClock clock,
    IUnitOfWork uow) :
    IRequestHandler<GetSalesReturn, SalesReturnDto>,
    IRequestHandler<CreateSalesReturn, SalesReturnDto>,
    IRequestHandler<DeleteSalesReturn, Unit>,
    IRequestHandler<PostSalesReturn, SalesReturnDto>,
    IRequestHandler<VoidSalesReturn, SalesReturnDto>
{
    public async Task<SalesReturnDto> Handle(GetSalesReturn q, CancellationToken ct) => ToDto(await LoadAsync(q.Id, ct));

    public async Task<SalesReturnDto> Handle(CreateSalesReturn cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var (order, invoice) = await SourceAsync(i.SalesOrderId, i.InvoiceId, ct);
        var partyId = invoice?.PartyId ?? order!.CustomerPartyId ?? throw new ConflictException("This sales order has no customer party.");
        var legalEntityId = invoice?.LegalEntityId ?? order!.LegalEntityId;
        await guard.RequireAsync(legalEntityId, ct);

        var ret = new SalesReturn
        {
            LegalEntityId = legalEntityId,
            SalesOrderId = order?.Id,
            OrderNumber = order?.Number,
            InvoiceId = invoice?.Id,
            InvoiceNumber = invoice?.Number,
            CustomerPartyId = partyId,
            CustomerName = invoice?.PartyName ?? order!.CustomerName,
            ReturnDate = i.ReturnDate,
            Reason = i.Reason.Trim(),
            Notes = i.Notes,
            Number = await numbers.NextAsync(Docs.SalesReturn, legalEntityId, i.ReturnDate, ct),
        };
        foreach (var l in i.Lines)
        {
            var (orderLine, invoiceLine) = Match(order, invoice, l.OrderLineId, l.InvoiceLineId);
            EnsureWithinDelivered(orderLine, l.Quantity);
            ret.Lines.Add(new SalesReturnLine
            {
                OrderLineId = orderLine?.Id,
                InvoiceLineId = invoiceLine?.Id,
                ItemId = orderLine?.ItemId ?? invoiceLine?.ItemId,
                ItemCode = orderLine?.ItemCode ?? string.Empty,
                Description = orderLine?.Description ?? invoiceLine!.Description,
                Quantity = l.Quantity,
                Restock = l.Restock,
            });
        }

        returns.Add(ret);
        await returns.SaveChangesAsync(ct);
        return ToDto(ret);
    }

    public async Task<Unit> Handle(DeleteSalesReturn cmd, CancellationToken ct)
    {
        var ret = await returns.GetForUpdateAsync(cmd.Id, ct);
        if (ret.Status != SalesReturnStatus.Draft) throw new ConflictException("Only a draft return can be deleted. Void it instead.");
        returns.Remove(ret);
        await returns.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public Task<SalesReturnDto> Handle(PostSalesReturn cmd, CancellationToken ct) => uow.InTransactionAsync(token => PostAsync(cmd, token), ct);

    private async Task<SalesReturnDto> PostAsync(PostSalesReturn cmd, CancellationToken ct)
    {
        var ret = await LoadAsync(cmd.Id, ct);
        if (ret.Status != SalesReturnStatus.Draft) throw new ConflictException("Only a draft return can be posted.");
        var (order, invoice) = await SourceAsync(ret.SalesOrderId, ret.InvoiceId, ct);

        if (invoice is not null)
        {
            var draft = await sender.Send(new CreateCreditNoteFromInvoice(invoice.Id, ret.ReturnDate, ret.Lines.Select(l =>
                new CreditNoteLineInput(l.InvoiceLineId ?? Match(order, invoice, l.OrderLineId, null).InvoiceLine!.Id, l.Quantity)).ToList(), $"Sales return {ret.Number}"), ct);
            var note = await sender.Send(new IssueInvoice(draft.Id), ct);
            ret.CreditNoteId = note.Id; ret.CreditNoteNumber = note.Number; ret.InvoiceId = invoice.Id; ret.InvoiceNumber = invoice.Number;
            ret.SubTotal = note.Net; ret.TaxTotal = note.Tax; ret.TotalAmount = note.Total;
        }

        foreach (var l in ret.Lines)
        {
            var orderLine = order?.Lines.FirstOrDefault(x => x.Id == (l.OrderLineId ?? Match(order, invoice, null, l.InvoiceLineId).OrderLine?.Id));
            if (orderLine is null) continue; // an invoice with no order behind it moved no stock
            EnsureWithinDelivered(orderLine, l.Quantity);
            if (l.Restock && l.ItemId is { } itemId)
                await stock.ReturnIssuedStockAsync(order!.Number, itemId, l.Quantity, ret.Number, ret.ReturnDate, ct);
            if (invoice is null) orderLine.DeliveredQuantity -= l.Quantity; // not billed yet: the order still owes these goods
            else orderLine.ReturnedQuantity += l.Quantity;
        }
        if (order is not null && invoice is null) RefreshStatus(order);

        ret.Status = SalesReturnStatus.Posted;
        await returns.SaveChangesAsync(ct);
        return ToDto(ret);
    }

    public Task<SalesReturnDto> Handle(VoidSalesReturn cmd, CancellationToken ct) => uow.InTransactionAsync(token => VoidAsync(cmd, token), ct);

    private async Task<SalesReturnDto> VoidAsync(VoidSalesReturn cmd, CancellationToken ct)
    {
        var ret = await LoadAsync(cmd.Id, ct);
        if (ret.Status != SalesReturnStatus.Posted) throw new ConflictException("Only a posted return can be voided.");

        if (ret.CreditNoteId is { } note) await sender.Send(new VoidInvoice(note, cmd.Reason), ct);
        await stock.ReverseReceiptAsync(ret.Number, ct);

        if (ret.SalesOrderId is { } orderId)
        {
            var order = await orders.Tracked(new Specification<SalesOrder>(o => o.Id == orderId)).Include(o => o.Lines).FirstOrDefaultAsync(ct);
            if (order is not null)
            {
                var invoice = ret.InvoiceId is { } invoiceId ? await invoices.Query(new Specification<Invoice>(x => x.Id == invoiceId).Include(x => x.Lines)).FirstOrDefaultAsync(ct) : null;
                foreach (var l in ret.Lines)
                {
                    var line = order.Lines.FirstOrDefault(x => x.Id == (l.OrderLineId ?? Match(order, invoice, null, l.InvoiceLineId).OrderLine?.Id));
                    if (line is null) continue;
                    if (ret.CreditNoteId is not null) line.ReturnedQuantity = Math.Max(0, line.ReturnedQuantity - l.Quantity);
                    else if (line.DeliveredQuantity + l.Quantity > line.Quantity)
                        throw new ConflictException($"'{line.Description}' has been delivered again since this return, so it cannot be voided.");
                    else line.DeliveredQuantity += l.Quantity;
                }
                if (ret.CreditNoteId is null) RefreshStatus(order);
            }
        }

        ret.Status = SalesReturnStatus.Voided;
        VoidMemo.Stamp(ret, cmd.Reason, clock, user);
        VoidMemo.Audit(audit, user, clock, nameof(SalesReturn), ret.Id, ret.Number, cmd.Reason!);
        await returns.SaveChangesAsync(ct);
        return ToDto(ret);
    }

    /// <summary>The order and invoice behind a return: naming one finds the other when the order was invoiced or the invoice came from an order.</summary>
    private async Task<(SalesOrder? Order, Invoice? Invoice)> SourceAsync(Guid? orderId, Guid? invoiceId, CancellationToken ct)
    {
        var order = orderId is { } oid
            ? await orders.Tracked(new Specification<SalesOrder>(o => o.Id == oid)).Include(o => o.Lines).FirstOrDefaultAsync(ct) ?? throw new FieldValidationException("salesOrderId", "Sales order not found.")
            : null;
        var linked = invoiceId ?? order?.InvoicedInvoiceId;
        var invoice = linked is { } iid
            ? await invoices.Query(new Specification<Invoice>(x => x.Id == iid).Include(x => x.Lines)).FirstOrDefaultAsync(ct) ?? throw new FieldValidationException("invoiceId", "Invoice not found.")
            : null;
        if (invoice is not null)
        {
            if (invoice.IsCreditNote) throw new ConflictException("A credit note cannot be returned against.");
            if (invoice.Status is not (DocumentStatus.Issued or DocumentStatus.PartlyPaid or DocumentStatus.Paid))
                throw new ConflictException($"Invoice {invoice.Number} is not issued. Issue or delete it before recording a return.");
            if (order is null && invoice.SourceType == OrderInvoiceVoidedHandler.SourceType && invoice.SourceId is { } sourceOrder)
                order = await orders.Tracked(new Specification<SalesOrder>(o => o.Id == sourceOrder)).Include(o => o.Lines).FirstOrDefaultAsync(ct);
        }
        return (order, invoice);
    }

    /// <summary>Pairs an order line with the invoice line the conversion made from it (same item, description prefixed with the order number).</summary>
    private static (SalesOrderLine? OrderLine, InvoiceLine? InvoiceLine) Match(SalesOrder? order, Invoice? invoice, Guid? orderLineId, Guid? invoiceLineId)
    {
        static bool Same(SalesOrder o, SalesOrderLine ol, InvoiceLine il) => il.ItemId == ol.ItemId && il.Description == $"{o.Number}: {ol.Description}";
        if (orderLineId is { } olid)
        {
            var ol = order?.Lines.FirstOrDefault(x => x.Id == olid) ?? throw new FieldValidationException("lines", "A line does not belong to this sales order.");
            var il = invoice?.Lines.FirstOrDefault(x => Same(order!, ol, x));
            if (invoice is not null && il is null) throw new ConflictException($"'{ol.Description}' is not on invoice {invoice.Number}.");
            return (ol, il);
        }
        if (invoiceLineId is { } ilid)
        {
            var il = invoice?.Lines.FirstOrDefault(x => x.Id == ilid) ?? throw new FieldValidationException("lines", "A line does not belong to this invoice.");
            return (order?.Lines.FirstOrDefault(x => Same(order, x, il)), il);
        }
        throw new FieldValidationException("lines", "Each line needs the order line or invoice line it returns.");
    }

    private static void EnsureWithinDelivered(SalesOrderLine? line, decimal quantity)
    {
        if (line is null) return;
        var left = line.DeliveredQuantity - line.ReturnedQuantity;
        if (quantity > left) throw new FieldValidationException("lines", $"Only {left:0.####} of '{line.Description}' has been delivered and not returned; cannot return {quantity:0.####}.");
    }

    private static void RefreshStatus(SalesOrder order)
    {
        if (order.Status is SalesOrderStatus.Invoiced or SalesOrderStatus.Cancelled or SalesOrderStatus.Draft) return;
        order.Status = order.Lines.Count > 0 && order.Lines.All(l => l.DeliveredQuantity >= l.Quantity) ? SalesOrderStatus.Delivered
            : order.Lines.Any(l => l.DeliveredQuantity > 0) ? SalesOrderStatus.Processing
            : SalesOrderStatus.Confirmed;
    }

    private async Task<SalesReturn> LoadAsync(Guid id, CancellationToken ct)
        => await returns.Tracked(new Specification<SalesReturn>(r => r.Id == id)).Include(r => r.Lines).FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Sales return '{id}' not found.");

    private static SalesReturnDto ToDto(SalesReturn r) => new(
        r.Id, r.Number, r.LegalEntityId, r.SalesOrderId, r.OrderNumber, r.InvoiceId, r.InvoiceNumber, r.CustomerPartyId, r.CustomerName, r.ReturnDate, r.Status, r.Reason, r.Notes,
        r.CreditNoteId, r.CreditNoteNumber, r.SubTotal, r.TaxTotal, r.TotalAmount,
        r.Lines.Select(l => new SalesReturnLineDto(l.Id, l.OrderLineId, l.InvoiceLineId, l.ItemId, l.ItemCode, l.Description, l.Quantity, l.Restock)).ToList(),
        r.VoidReason, r.VoidedAt, r.CreatedAt);
}
