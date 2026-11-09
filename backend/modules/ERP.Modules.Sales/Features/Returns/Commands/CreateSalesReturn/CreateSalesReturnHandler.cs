using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Repositories;

namespace ERP.Modules.Sales.Features;

internal sealed class CreateSalesReturnHandler(
    ISalesReturnRepository returns,
    SalesReturnSources sources,
    ILegalEntityGuard guard,
    INumberingService numbers) : ICommandHandler<CreateSalesReturnCommand, SalesReturnDto>
{
    public async Task<SalesReturnDto> Handle(CreateSalesReturnCommand cmd, CancellationToken ct)
    {
        // 1. The order or invoice, its customer and legal entity.
        var i = cmd.Input;
        var (order, invoice) = await sources.SourceAsync(i.SalesOrderId, i.InvoiceId, ct);
        var partyId = invoice?.PartyId ?? order!.CustomerPartyId ?? throw new ConflictException("This sales order has no customer party.");
        var legalEntityId = invoice?.LegalEntityId ?? order!.LegalEntityId;
        await guard.RequireAsync(legalEntityId, ct);

        // 2. The draft return, each line matched to the order and invoice and within what was delivered.
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
            var (orderLine, invoiceLine) = SalesReturnSources.Match(order, invoice, l.OrderLineId, l.InvoiceLineId);
            SalesReturnSources.EnsureWithinDelivered(orderLine, l.Quantity);
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
        return SalesReturnDto.From(ret);
    }
}
