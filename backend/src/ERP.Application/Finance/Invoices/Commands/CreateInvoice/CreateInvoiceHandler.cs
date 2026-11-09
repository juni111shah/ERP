using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class CreateInvoiceHandler(IInvoiceRepository invoices, IInvoicingService invoicing) : ICommandHandler<CreateInvoiceCommand, InvoiceDto>
{
    public async Task<InvoiceDto> Handle(CreateInvoiceCommand command, CancellationToken ct)
    {
        // The invoicing service numbers the draft, prices the lines and adds it to the unit of work.
        var invoice = await invoicing.BuildAsync(command.Input.ToDraft(), ct);
        await invoices.SaveChangesAsync(ct);
        return InvoiceDto.From(invoice);
    }
}
