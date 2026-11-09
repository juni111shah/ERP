using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Platform;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Repositories;
using MediatR;

namespace ERP.Modules.Sales.Features;

internal sealed class VoidSalesReturnHandler(
    ISalesReturnRepository returns,
    ISalesOrderRepository orders,
    ISalesInvoiceRepository invoices,
    IStockPostingService stock,
    ISender sender,
    IRepository<AuditEvent> audit,
    ICurrentUser user,
    IClock clock,
    IUnitOfWork uow) : ICommandHandler<VoidSalesReturnCommand, SalesReturnDto>
{
    public Task<SalesReturnDto> Handle(VoidSalesReturnCommand cmd, CancellationToken ct) => uow.InTransactionAsync(token => VoidAsync(cmd, token), ct);

    private async Task<SalesReturnDto> VoidAsync(VoidSalesReturnCommand cmd, CancellationToken ct)
    {
        // 1. Only a posted return can be voided.
        var ret = await returns.GetWithLinesForUpdateAsync(cmd.Id, ct);
        if (ret.Status != SalesReturnStatus.Posted) throw new ConflictException("Only a posted return can be voided.");

        // 2. Void the credit note and reverse the stock.
        if (ret.CreditNoteId is { } note) await sender.Send(new VoidInvoiceCommand(note, cmd.Reason), ct);
        await stock.ReverseReceiptAsync(ret.Number, ct);

        // 3. Put the quantities back on the order.
        if (ret.SalesOrderId is { } orderId)
        {
            var order = await orders.FindWithLinesForUpdateAsync(orderId, ct);
            if (order is not null)
            {
                var invoice = ret.InvoiceId is { } invoiceId ? await invoices.FindWithLinesAsync(invoiceId, ct) : null;
                foreach (var l in ret.Lines)
                {
                    var line = order.Lines.FirstOrDefault(x => x.Id == (l.OrderLineId ?? SalesReturnSources.Match(order, invoice, null, l.InvoiceLineId).OrderLine?.Id));
                    if (line is null) continue;
                    if (ret.CreditNoteId is not null) line.ReturnedQuantity = Math.Max(0, line.ReturnedQuantity - l.Quantity);
                    else if (line.DeliveredQuantity + l.Quantity > line.Quantity)
                        throw new ConflictException($"'{line.Description}' has been delivered again since this return, so it cannot be voided.");
                    else line.DeliveredQuantity += l.Quantity;
                }
                if (ret.CreditNoteId is null) SalesReturnSources.RefreshStatus(order);
            }
        }

        // 4. Mark it voided and audit the reason.
        ret.Status = SalesReturnStatus.Voided;
        VoidMemo.Stamp(ret, cmd.Reason, clock, user);
        VoidMemo.Audit(audit, user, clock, nameof(SalesReturn), ret.Id, ret.Number, cmd.Reason!);
        await returns.SaveChangesAsync(ct);
        return SalesReturnDto.From(ret);
    }
}
