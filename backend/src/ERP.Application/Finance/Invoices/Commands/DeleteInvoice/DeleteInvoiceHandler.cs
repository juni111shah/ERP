using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Finance;
using MediatR;

namespace ERP.Application.Finance;

internal sealed class DeleteInvoiceHandler(IInvoiceRepository invoices, IUnitOfWork unitOfWork, IPublisher publisher) : ICommandHandler<DeleteInvoiceCommand>
{
    // The handlers of InvoiceVoided may move stock and post: they commit with the invoice or not at all.
    public Task<Unit> Handle(DeleteInvoiceCommand command, CancellationToken ct) => unitOfWork.InTransactionAsync(token => DeleteAsync(command, token), ct);

    private async Task<Unit> DeleteAsync(DeleteInvoiceCommand command, CancellationToken ct)
    {
        var invoice = await invoices.GetWithLinesForUpdateAsync(command.Id, ct);
        if (invoice.Status != DocumentStatus.Draft) throw new ConflictException("Only a draft can be deleted.");

        // The record it was raised from (milestone, sales order, instalment...) must become billable again, as when an issued invoice is voided.
        if (invoice.SourceType is not null) await publisher.Publish(new InvoiceVoided(invoice.Id, invoice.SourceType, invoice.SourceId), ct);

        invoices.Remove(invoice);
        await invoices.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
