using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetInvoiceByIdHandler(IInvoiceRepository invoices) : IQueryHandler<GetInvoiceByIdQuery, InvoiceDto>
{
    public async Task<InvoiceDto> Handle(GetInvoiceByIdQuery query, CancellationToken ct)
        => InvoiceDto.From(await invoices.GetWithLinesAsync(query.Id, ct));
}
