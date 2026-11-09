using ERP.Application.Common;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

internal sealed class GetPosShiftReportHandler(
    IPosSessionRepository sessions,
    IPosOrderRepository orders,
    IPosReturnRepository returns,
    ICashDrawerTransactionRepository drawer) : IQueryHandler<GetPosShiftReportQuery, PosShiftReportDto>
{
    public async Task<PosShiftReportDto> Handle(GetPosShiftReportQuery request, CancellationToken ct)
    {
        // 1. The shift with its drawer movements in time order.
        var id = request.SessionId;
        var session = await sessions.FindAsync(id, ct)
            ?? throw new NotFoundException($"Session {id} not found");
        session.DrawerTransactions = (await drawer.ListForSessionAsync(id, ct)).OrderBy(t => t.Timestamp).ToList();

        // 2. The documents of the shift.
        var docs = await orders.ListForSessionAsync(id, ct);
        var sales = docs.Where(o => o.Status == PosOrderStatus.Completed).ToList();
        var voids = docs.Where(o => o.Status == PosOrderStatus.Voided).ToList();
        var given = await returns.ListForSessionAsync(id, ct);
        var heldCount = await orders.CountAsync(o => o.RegisterId == session.RegisterId && o.Status == PosOrderStatus.Parked, ct);

        // 3. The figures.
        var gross = sales.Sum(o => o.SubTotal + o.DiscountTotal);
        var salesTotal = sales.Sum(o => o.TotalAmount);
        var returned = given.Sum(r => r.TotalAmount);

        return new PosShiftReportDto(
            PosSessionDto.From(session),
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
            PosReportFigures.Tenders(sales, given),
            PosReportFigures.TopItems(sales));
    }
}
