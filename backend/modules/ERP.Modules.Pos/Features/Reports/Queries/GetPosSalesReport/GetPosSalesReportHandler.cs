using ERP.Application.Common;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

internal sealed class GetPosSalesReportHandler(
    IPosSessionRepository sessions,
    IPosOrderRepository orders,
    IPosReturnRepository returns,
    IPosRegisterRepository registers,
    PosCalendar calendar) : IQueryHandler<GetPosSalesReportQuery, PosSalesReportDto>
{
    public async Task<PosSalesReportDto> Handle(GetPosSalesReportQuery request, CancellationToken ct)
    {
        // 1. The range, in the shop's own days.
        var today = await calendar.TodayAsync(ct);
        var to = request.To ?? request.From ?? today;
        var from = request.From ?? to;
        var (start, end) = await calendar.RangeAsync(from, to, ct);
        var zone = await calendar.ZoneAsync(ct);

        // 2. The documents of the range; the grouping below happens here, in the shop's own time zone.
        var entity = request.LegalEntityId;
        var register = request.RegisterId;
        var docs = await orders.ListInRangeAsync(start, end, entity, register, ct);
        var given = await returns.ListInRangeAsync(start, end, entity, register, ct);
        var closed = await sessions.ListClosedCashDifferencesAsync(start, end, entity, register, ct);

        var sales = docs.Where(o => o.Status == PosOrderStatus.Completed).ToList();
        var voids = docs.Where(o => o.Status == PosOrderStatus.Voided).ToList();

        DateTime Local(DateTimeOffset at) => TimeZoneInfo.ConvertTimeFromUtc(at.UtcDateTime, zone);

        // 3. By day and by hour.
        var days = new Dictionary<DateOnly, (decimal Sales, decimal Returns, int Count)>();
        foreach (var o in sales)
        {
            var d = DateOnly.FromDateTime(Local(o.OrderDate));
            var row = days.GetValueOrDefault(d);
            days[d] = (row.Sales + o.TotalAmount, row.Returns, row.Count + 1);
        }
        foreach (var r in given)
        {
            var d = DateOnly.FromDateTime(Local(r.ReturnDate));
            var row = days.GetValueOrDefault(d);
            days[d] = (row.Sales, row.Returns + r.TotalAmount, row.Count);
        }

        var hours = sales.GroupBy(o => Local(o.OrderDate).Hour)
            .Select(g => new PosSalesHourDto(g.Key, g.Sum(o => o.TotalAmount), g.Count())).OrderBy(h => h.Hour).ToList();

        var registerNames = await registers.NamesAsync(ct);
        string RegisterName(Guid id) => registerNames.TryGetValue(id, out var n) ? n : "Removed register";

        // 4. The totals.
        var salesTotal = sales.Sum(o => o.TotalAmount);
        var returned = given.Sum(r => r.TotalAmount);

        return new PosSalesReportDto(
            from,
            to,
            sales.Sum(o => o.SubTotal + o.DiscountTotal),
            sales.Sum(o => o.DiscountTotal),
            sales.Sum(o => o.SubTotal),
            sales.Sum(o => o.TaxTotal),
            salesTotal,
            returned,
            salesTotal - returned,
            sales.Count,
            voids.Count,
            voids.Sum(o => o.TotalAmount),
            given.Count,
            sales.Count > 0 ? Math.Round(salesTotal / sales.Count, 2) : 0m,
            closed.Sum(),
            days.OrderBy(d => d.Key).Select(d => new PosSalesDayDto(d.Key, d.Value.Sales, d.Value.Returns, d.Value.Sales - d.Value.Returns, d.Value.Count)).ToList(),
            hours,
            PosReportFigures.Tenders(sales, given),
            PosReportFigures.Groups(sales, given, o => RegisterName(o.RegisterId), r => RegisterName(r.RegisterId)),
            PosReportFigures.Groups(sales, given, o => string.IsNullOrWhiteSpace(o.CashierName) ? "Unknown" : o.CashierName, r => string.IsNullOrWhiteSpace(r.CashierName) ? "Unknown" : r.CashierName),
            PosReportFigures.TopItems(sales));
    }
}
