using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Repositories;
using MediatR;

namespace ERP.Modules.Sales.Features;

internal sealed class PostSalesReturnHandler(
    ISalesReturnRepository returns,
    SalesReturnSources sources,
    IStockPostingService stock,
    ISender sender,
    IUnitOfWork uow) : ICommandHandler<PostSalesReturnCommand, SalesReturnDto>
{
    public Task<SalesReturnDto> Handle(PostSalesReturnCommand cmd, CancellationToken ct) => uow.InTransactionAsync(token => PostAsync(cmd, token), ct);

    private async Task<SalesReturnDto> PostAsync(PostSalesReturnCommand cmd, CancellationToken ct)
    {
        // 1. Only a draft return can be posted.
        var ret = await returns.GetWithLinesForUpdateAsync(cmd.Id, ct);
        if (ret.Status != SalesReturnStatus.Draft) throw new ConflictException("Only a draft return can be posted.");
        var (order, invoice) = await sources.SourceAsync(ret.SalesOrderId, ret.InvoiceId, ct);

        // 2. Invoiced goods: issue the credit note for the returned lines.
        if (invoice is not null)
        {
            var draft = await sender.Send(new CreateCreditNoteFromInvoiceCommand(invoice.Id, ret.ReturnDate, ret.Lines.Select(l =>
                new CreditNoteLineInput(l.InvoiceLineId ?? SalesReturnSources.Match(order, invoice, l.OrderLineId, null).InvoiceLine!.Id, l.Quantity)).ToList(), $"Sales return {ret.Number}"), ct);
            var note = await sender.Send(new IssueInvoiceCommand(draft.Id), ct);
            ret.CreditNoteId = note.Id; ret.CreditNoteNumber = note.Number; ret.InvoiceId = invoice.Id; ret.InvoiceNumber = invoice.Number;
            ret.SubTotal = note.Net; ret.TaxTotal = note.Tax; ret.TotalAmount = note.Total;
        }

        // 3. Restock and update the order's delivered or returned quantities.
        foreach (var l in ret.Lines)
        {
            var orderLine = order?.Lines.FirstOrDefault(x => x.Id == (l.OrderLineId ?? SalesReturnSources.Match(order, invoice, null, l.InvoiceLineId).OrderLine?.Id));
            if (orderLine is null) continue; // an invoice with no order behind it moved no stock
            SalesReturnSources.EnsureWithinDelivered(orderLine, l.Quantity);
            if (l.Restock && l.ItemId is { } itemId)
                await stock.ReturnIssuedStockAsync(order!.Number, itemId, l.Quantity, ret.Number, ret.ReturnDate, ct);
            if (invoice is null) orderLine.DeliveredQuantity -= l.Quantity; // not billed yet: the order still owes these goods
            else orderLine.ReturnedQuantity += l.Quantity;
        }
        if (order is not null && invoice is null) SalesReturnSources.RefreshStatus(order);

        ret.Status = SalesReturnStatus.Posted;
        await returns.SaveChangesAsync(ct);
        return SalesReturnDto.From(ret);
    }
}
