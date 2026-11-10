using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetCustomerStatementHandler(ReceivablesAsOf receivables, IReportCurrencyGuard currencies) : IQueryHandler<GetCustomerStatementQuery, CustomerStatementDto>
{
    public async Task<CustomerStatementDto> Handle(GetCustomerStatementQuery query, CancellationToken ct)
    {
        await currencies.EnsureSingleCurrencyAsync(query.LegalEntityId, ct);
        var open = await receivables.OpenAsync(query.AsOf, query.LegalEntityId, query.PartyId, ct);
        var unallocated = (await receivables.UnallocatedAsync(query.AsOf, query.LegalEntityId, query.PartyId, ct)).Sum(u => u.Amount);
        var items = open.OrderBy(o => o.DueDate).Select(o => new AgeingItem(o.Id, o.Number, o.IsCreditNote, o.InvoiceDate, o.DueDate, Math.Max(0, query.AsOf.DayNumber - o.DueDate.DayNumber), o.Total - o.Paid)).ToList();
        return new CustomerStatementDto(query.PartyId, open.FirstOrDefault()?.PartyName, query.AsOf, items, unallocated, open.Sum(ReceivablesAsOf.Signed) - unallocated);
    }
}
