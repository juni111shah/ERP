using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Pos.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Features;

public record PosTenderRowDto(PosPaymentMethod Method, decimal Sales, decimal Refunds, decimal Net, int Count);

public record PosTopItemDto(string ItemCode, string Description, decimal Quantity, decimal Revenue);

/// <summary>Shift report: an X report while the shift is open, the Z report once it closes. Figures come from the documents of the shift.</summary>
public record PosShiftReportDto(
    PosSessionDto Session,
    decimal GrossSales,
    decimal Discounts,
    decimal NetSales,
    decimal Tax,
    decimal SalesTotal,
    decimal Returns,
    decimal Voids,
    decimal NetTakings,
    int SaleCount,
    int VoidCount,
    int ReturnCount,
    decimal ItemsSold,
    decimal AverageTicket,
    decimal CashSales,
    decimal CashRefunds,
    int HeldCount,
    List<PosTenderRowDto> Tenders,
    List<PosTopItemDto> TopItems);

public record GetPosShiftReportQuery(Guid SessionId) : IQuery<PosShiftReportDto>;

public record PosSalesDayDto(DateOnly Date, decimal Sales, decimal Returns, decimal Net, int Count);
public record PosSalesHourDto(int Hour, decimal Sales, int Count);
public record PosSalesGroupDto(string Name, decimal Sales, decimal Returns, decimal Net, int Count);

public record PosSalesReportDto(
    DateOnly From,
    DateOnly To,
    decimal GrossSales,
    decimal Discounts,
    decimal NetSales,
    decimal Tax,
    decimal SalesTotal,
    decimal Returns,
    decimal NetTakings,
    int SaleCount,
    int VoidCount,
    decimal VoidTotal,
    int ReturnCount,
    decimal AverageTicket,
    decimal CashOverShort,
    List<PosSalesDayDto> Days,
    List<PosSalesHourDto> Hours,
    List<PosTenderRowDto> Tenders,
    List<PosSalesGroupDto> Registers,
    List<PosSalesGroupDto> Cashiers,
    List<PosTopItemDto> TopItems);

public record GetPosSalesReportQuery(DateOnly? From, DateOnly? To, Guid? RegisterId, Guid? LegalEntityId) : IQuery<PosSalesReportDto>;

public class GetPosSalesReportValidator : AbstractValidator<GetPosSalesReportQuery>
{
    public const int MaxDays = 366;

    public GetPosSalesReportValidator()
    {
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.To >= x.From).WithName("to").WithMessage("The end date must be on or after the start date.");
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.To.Value.DayNumber - x.From.Value.DayNumber < MaxDays).WithName("to").WithMessage("The report covers at most one year.");
    }
}

public class PosReportHandlers(
    IReadRepository<PosSession> sessions,
    IReadRepository<PosOrder> orders,
    IReadRepository<PosReturn> returns,
    IReadRepository<CashDrawerTransaction> drawer,
    IReadRepository<PosRegister> registers,
    PosCalendar calendar) :
    IRequestHandler<GetPosShiftReportQuery, PosShiftReportDto>,
    IRequestHandler<GetPosSalesReportQuery, PosSalesReportDto>
{
    private const int TopItemCount = 10;

    public async Task<PosShiftReportDto> Handle(GetPosShiftReportQuery request, CancellationToken ct)
    {
        var id = request.SessionId;
        var session = await sessions.Query(new Specification<PosSession>(s => s.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Session {id} not found");
        session.DrawerTransactions = (await drawer.Query(new Specification<CashDrawerTransaction>(t => t.SessionId == id)).ToListAsync(ct)).OrderBy(t => t.Timestamp).ToList();

        var docs = await orders.Query(new Specification<PosOrder>(o => o.SessionId == id).Include(o => o.Lines).Include(o => o.Payments)).ToListAsync(ct);
        var sales = docs.Where(o => o.Status == PosOrderStatus.Completed).ToList();
        var voids = docs.Where(o => o.Status == PosOrderStatus.Voided).ToList();
        var given = await returns.Query(new Specification<PosReturn>(r => r.SessionId == id).Include(r => r.Refunds)).ToListAsync(ct);
        var heldCount = await orders.CountAsync(o => o.RegisterId == session.RegisterId && o.Status == PosOrderStatus.Parked, ct);

        var gross = sales.Sum(o => o.SubTotal + o.DiscountTotal);
        var salesTotal = sales.Sum(o => o.TotalAmount);
        var returned = given.Sum(r => r.TotalAmount);

        return new PosShiftReportDto(
            SessionHandlers.Map(session),
            gross,
            sales.Sum(o => o.DiscountTotal),
            sales.Sum(o => o.SubTotal),
            sales.Sum(o => o.TaxTotal),
            salesTotal,
            returned,
            voids.Sum(o => o.TotalAmount),
            salesTotal - returned,
            sales.Count,
            voids.Count,
            given.Count,
            sales.SelectMany(o => o.Lines).Sum(l => l.Quantity),
            sales.Count > 0 ? Math.Round(salesTotal / sales.Count, 2) : 0m,
            sales.SelectMany(o => o.Payments).Where(p => p.Method == PosPaymentMethod.Cash).Sum(p => p.Amount),
            given.SelectMany(r => r.Refunds).Where(f => f.Method == PosPaymentMethod.Cash).Sum(f => f.Amount),
            heldCount,
            Tenders(sales, given),
            TopItems(sales));
    }

    public async Task<PosSalesReportDto> Handle(GetPosSalesReportQuery request, CancellationToken ct)
    {
        var today = await calendar.TodayAsync(ct);
        var to = request.To ?? request.From ?? today;
        var from = request.From ?? to;
        var (start, end) = await calendar.RangeAsync(from, to, ct);
        var zone = await calendar.ZoneAsync(ct);

        var entity = request.LegalEntityId;
        var register = request.RegisterId;
        // CreatedAt carries the order date and compares in SQL on every provider; the grouping below happens here, in the shop's own time zone.
        var docs = await orders.Query(new Specification<PosOrder>(o => o.CreatedAt >= start && o.CreatedAt < end
                && o.Status != PosOrderStatus.Parked
                && (entity == null || o.LegalEntityId == entity) && (register == null || o.RegisterId == register))
            .Include(o => o.Lines).Include(o => o.Payments)).ToListAsync(ct);
        var given = await returns.Query(new Specification<PosReturn>(r => r.CreatedAt >= start && r.CreatedAt < end
                && (entity == null || r.LegalEntityId == entity) && (register == null || r.RegisterId == register))
            .Include(r => r.Refunds)).ToListAsync(ct);
        var closed = await sessions.Query(new Specification<PosSession>(s => s.ClosedAt != null && s.Status == PosSessionStatus.Closed
                && s.CreatedAt >= start && s.CreatedAt < end
                && (entity == null || s.LegalEntityId == entity) && (register == null || s.RegisterId == register)))
            .Select(s => s.CashDifference).ToListAsync(ct);

        var sales = docs.Where(o => o.Status == PosOrderStatus.Completed).ToList();
        var voids = docs.Where(o => o.Status == PosOrderStatus.Voided).ToList();

        DateTime Local(DateTimeOffset at) => TimeZoneInfo.ConvertTimeFromUtc(at.UtcDateTime, zone);

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

        var registerNames = await registers.Query(new Specification<PosRegister>(r => true)).Select(r => new { r.Id, r.Name }).ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        string RegisterName(Guid id) => registerNames.TryGetValue(id, out var n) ? n : "Removed register";

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
            Tenders(sales, given),
            Groups(sales, given, o => RegisterName(o.RegisterId), r => RegisterName(r.RegisterId)),
            Groups(sales, given, o => string.IsNullOrWhiteSpace(o.CashierName) ? "Unknown" : o.CashierName, r => string.IsNullOrWhiteSpace(r.CashierName) ? "Unknown" : r.CashierName),
            TopItems(sales));
    }

    private static List<PosTenderRowDto> Tenders(IEnumerable<PosOrder> sales, IEnumerable<PosReturn> given)
    {
        var paid = sales.SelectMany(o => o.Payments).GroupBy(p => p.Method).ToDictionary(g => g.Key, g => (Amount: g.Sum(p => p.Amount), Count: g.Count()));
        var back = given.SelectMany(r => r.Refunds).GroupBy(f => f.Method).ToDictionary(g => g.Key, g => g.Sum(f => f.Amount));
        return paid.Keys.Union(back.Keys).OrderBy(m => m).Select(m =>
        {
            var p = paid.GetValueOrDefault(m);
            var b = back.GetValueOrDefault(m);
            return new PosTenderRowDto(m, p.Amount, b, p.Amount - b, p.Count);
        }).ToList();
    }

    private static List<PosSalesGroupDto> Groups(IEnumerable<PosOrder> sales, IEnumerable<PosReturn> given, Func<PosOrder, string> saleKey, Func<PosReturn, string> returnKey)
    {
        var sold = sales.GroupBy(saleKey).ToDictionary(g => g.Key, g => (Amount: g.Sum(o => o.TotalAmount), Count: g.Count()));
        var back = given.GroupBy(returnKey).ToDictionary(g => g.Key, g => g.Sum(r => r.TotalAmount));
        return sold.Keys.Union(back.Keys).Select(k =>
        {
            var s = sold.GetValueOrDefault(k);
            var b = back.GetValueOrDefault(k);
            return new PosSalesGroupDto(k, s.Amount, b, s.Amount - b, s.Count);
        }).OrderByDescending(g => g.Net).ToList();
    }

    private static List<PosTopItemDto> TopItems(IEnumerable<PosOrder> sales) => sales.SelectMany(o => o.Lines)
        .GroupBy(l => (l.ItemCode, l.Description))
        .Select(g => new PosTopItemDto(g.Key.ItemCode, g.Key.Description, g.Sum(l => l.Quantity), g.Sum(l => l.LineTotal)))
        .OrderByDescending(i => i.Revenue).Take(TopItemCount).ToList();
}
