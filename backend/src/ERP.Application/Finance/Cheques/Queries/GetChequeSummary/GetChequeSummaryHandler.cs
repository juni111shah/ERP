using ERP.Application.Common;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

internal sealed class GetChequeSummaryHandler(IPaymentInstrumentRepository instruments) : IQueryHandler<GetChequeSummaryQuery, ChequeSummaryDto>
{
    public async Task<ChequeSummaryDto> Handle(GetChequeSummaryQuery query, CancellationToken ct)
    {
        var rows = await instruments.ListReceivedForSummaryAsync(ct);
        var held = rows.Where(r => r.Status == InstrumentStatus.InCustody).ToList();
        return new ChequeSummaryDto(
            held.Sum(r => r.Amount), held.Count,
            held.Where(r => r.InstrumentDate <= query.AsOf).Sum(r => r.Amount), held.Count(r => r.InstrumentDate <= query.AsOf),
            held.Where(r => r.InstrumentDate > query.AsOf && r.InstrumentDate <= query.AsOf.AddDays(7)).Sum(r => r.Amount),
            held.Where(r => r.InstrumentDate > query.AsOf && r.InstrumentDate <= query.AsOf.AddDays(30)).Sum(r => r.Amount),
            rows.Where(r => r.Status == InstrumentStatus.Deposited).Sum(r => r.Amount), rows.Count(r => r.Status == InstrumentStatus.Bounced));
    }
}
