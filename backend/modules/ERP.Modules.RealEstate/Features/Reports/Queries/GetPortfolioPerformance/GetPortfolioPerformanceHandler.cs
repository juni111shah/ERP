using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetPortfolioPerformanceHandler(
    IPropertyRepository properties, IUnitRepository units, IRealEstateInvoiceRepository invoices, IRentInstalmentRepository instalments, ILeaseRepository leases,
    IRealEstateSettlementRepository settlements, IWorkOrderRepository workOrders)
    : IQueryHandler<GetPortfolioPerformanceQuery, PerformanceDto>
{
    public async Task<PerformanceDto> Handle(GetPortfolioPerformanceQuery q, CancellationToken ct)
    {
        // 1. The active properties and their units.
        var entity = q.LegalEntityId;
        var props = await properties.ListActiveValuesAsync(entity, ct);
        var unitRows = await units.ListStatusesAsync(entity, ct);

        // 2. rent invoices -> instalment -> lease -> property
        var rentInvoices = await invoices.ListPostedOfSourceAsync(RentInvoicer.SourceType, q.From, q.To, entity, ct);
        var instalmentIds = rentInvoices.Where(i => i.SourceId != null).Select(i => i.SourceId!.Value).ToList();
        var instalmentLease = await instalments.MapLeaseIdsAsync(instalmentIds, ct);
        var leaseIds = instalmentLease.Values.Distinct().ToList();
        var leaseProperty = await leases.MapPropertyIdsAsync(leaseIds, ct);
        Guid? PropertyOf(Guid? instalmentId) => instalmentId is { } id && instalmentLease.TryGetValue(id, out var lease) && leaseProperty.TryGetValue(lease, out var p) ? p : null;
        var invoiceProperty = rentInvoices.ToDictionary(i => i.Id, i => PropertyOf(i.SourceId));

        // 3. What was received on those invoices in the period.
        var invoiceIds = rentInvoices.Select(i => i.Id).ToList();
        var collected = (await settlements.ListReceivedAllocationsAsync(q.From, q.To, ct))
            .Where(a => a.InvoiceId != null && invoiceProperty.ContainsKey(a.InvoiceId.Value)).ToList();
        // billed figures are before tax, so what is collected against them is too; receipts also carry the tax
        var invoiceMoney = rentInvoices.ToDictionary(i => i.Id, i => (i.Net, i.Total));
        var collectedNet = collected.GroupBy(a => a.InvoiceId!.Value).Select(g => new { InvoiceId = g.Key, Amount = RentInReceipts.NetPart(g.Sum(a => a.Amount), invoiceMoney[g.Key].Net, invoiceMoney[g.Key].Total) }).ToList();

        // 4. The operating expenses: actual cost of the work completed in the period.
        var costs = await workOrders.ActualCostsByPropertyAsync(q.From, q.To, entity, ct);

        // 5. One row per property, then the portfolio.
        var days = Math.Max(1, q.To.DayNumber - q.From.DayNumber + 1);
        PerformanceRow Build(Guid id, string name, Guid entityId, decimal value, decimal occupancy, decimal income, decimal opex, decimal billed, decimal collectedAmount)
        {
            var noi = income - opex;
            return new PerformanceRow(id, name, entityId, value, occupancy, income, opex, noi, value > 0 ? Math.Round(noi * 365m / days / value * 100m, 2) : 0, billed, collectedAmount, billed > 0 ? Math.Round(100m * collectedAmount / billed, 1) : 0);
        }
        var rows = props.Select(p =>
        {
            var mine = unitRows.Where(u => u.PropertyId == p.Id).ToList();
            var occupancy = mine.Count == 0 ? 0 : Math.Round(100m * mine.Count(u => u.Status == UnitStatus.Occupied) / mine.Count, 1);
            var billed = rentInvoices.Where(i => invoiceProperty[i.Id] == p.Id).Sum(i => i.IsCreditNote ? -i.Net : i.Net);
            var paid = collectedNet.Where(a => invoiceProperty[a.InvoiceId] == p.Id).Sum(a => a.Amount);
            return Build(p.Id, p.Name, p.LegalEntityId, p.IndicativeValue, occupancy, billed, costs.GetValueOrDefault(p.Id), billed, paid);
        }).ToList();
        var all = unitRows.Count == 0 ? 0 : Math.Round(100m * unitRows.Count(u => u.Status == UnitStatus.Occupied) / unitRows.Count, 1);
        return new PerformanceDto(q.From, q.To, rows, Build(Guid.Empty, "Portfolio", Guid.Empty, rows.Sum(r => r.Value), all, rows.Sum(r => r.Income), rows.Sum(r => r.OperatingExpenses), rows.Sum(r => r.Billed), rows.Sum(r => r.Collected)));
    }
}
