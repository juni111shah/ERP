using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetInvoiceAllocationsHandler(ISettlementRepository settlements) : IQueryHandler<GetInvoiceAllocationsQuery, IReadOnlyList<InvoiceAllocationDto>>
{
    public async Task<IReadOnlyList<InvoiceAllocationDto>> Handle(GetInvoiceAllocationsQuery query, CancellationToken ct)
    {
        var receipts = await settlements.ListReceiptsAllocatedToInvoiceAsync(query.InvoiceId, ct);
        return receipts.Select(s => new InvoiceAllocationDto(
            s.Id, s.Number, s.Date, s.Method, s.Reference, s.CurrencyCode,
            Amount: s.Allocations.Where(a => a.InvoiceId == query.InvoiceId).Sum(a => a.Amount),
            s.Status)).ToList();
    }
}
