using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetVendorStatementHandler(PayablesAsOf payables, IReportCurrencyGuard currencies) : IQueryHandler<GetVendorStatementQuery, VendorStatementDto>
{
    public async Task<VendorStatementDto> Handle(GetVendorStatementQuery query, CancellationToken ct)
    {
        await currencies.EnsureSingleCurrencyAsync(query.LegalEntityId, ct);
        var open = await payables.OpenAsync(query.AsOf, query.LegalEntityId, query.PartyId, ct);
        var advances = (await payables.AdvancesAsync(query.AsOf, query.LegalEntityId, query.PartyId, ct)).Sum(a => a.Amount);
        var items = open.OrderBy(o => o.DueDate).Select(o => new AgeingItem(o.Id, o.Number, false, o.BillDate, o.DueDate, Math.Max(0, query.AsOf.DayNumber - o.DueDate.DayNumber), o.Total - o.Paid)).ToList();
        return new VendorStatementDto(query.PartyId, open.FirstOrDefault()?.PartyName, query.AsOf, items, advances, open.Sum(o => o.Total - o.Paid) - advances);
    }
}
