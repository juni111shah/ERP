using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Finance;

internal sealed class VoidInvoiceHandler(
    IInvoiceRepository invoices,
    IInvoicingService invoicing,
    IRepository<AuditEvent> audit,
    ICurrentUser user,
    IClock clock,
    IUnitOfWork unitOfWork)
    : ICommandHandler<VoidInvoiceCommand, InvoiceDto>
{
    // The handlers of InvoiceVoided may move stock and post: they commit with the invoice or not at all.
    public Task<InvoiceDto> Handle(VoidInvoiceCommand command, CancellationToken ct) => unitOfWork.InTransactionAsync(token => VoidAsync(command, token), ct);

    private async Task<InvoiceDto> VoidAsync(VoidInvoiceCommand command, CancellationToken ct)
    {
        var invoice = await invoices.GetWithLinesForUpdateAsync(command.Id, ct);
        await invoicing.VoidAsync(invoice, command.Reason, ct);
        VoidMemo.Audit(audit, user, clock, nameof(Domain.Finance.Invoice), invoice.Id, invoice.Number, command.Reason!);
        await invoices.SaveChangesAsync(ct);
        return InvoiceDto.From(invoice);
    }
}
