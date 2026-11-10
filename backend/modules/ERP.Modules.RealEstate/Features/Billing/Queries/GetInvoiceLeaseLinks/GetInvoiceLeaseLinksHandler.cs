using ERP.Application.Common;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetInvoiceLeaseLinksHandler(IRentInstalmentRepository instalments, ILeaseRepository leases, IUnitRepository units, IPropertyRepository properties)
    : IQueryHandler<GetInvoiceLeaseLinksQuery, IReadOnlyList<InvoiceLeaseLinkDto>>
{
    public async Task<IReadOnlyList<InvoiceLeaseLinkDto>> Handle(GetInvoiceLeaseLinksQuery q, CancellationToken ct)
    {
        // 1. The instalments invoiced by those invoices.
        var ids = q.InvoiceIds.Distinct().Take(200).ToList();
        if (ids.Count == 0) return [];
        var inst = await instalments.ListOfInvoicesAsync(ids, ct);

        // 2. Their leases, with property names and unit numbers.
        var leaseIds = inst.Select(i => i.LeaseId).Distinct().ToList();
        var leaseRows = await leases.ListByIdsAsync(leaseIds, ct);
        var propIds = leaseRows.Select(l => l.PropertyId).Distinct().ToList();
        var unitIds = leaseRows.Select(l => l.UnitId).Distinct().ToList();
        var props = await properties.NamesAsync(propIds, ct);
        var unitNos = await units.NumbersAsync(unitIds, ct);

        // 3. One link per invoiced instalment.
        var byLease = leaseRows.ToDictionary(l => l.Id);
        return inst.Where(i => byLease.ContainsKey(i.LeaseId)).Select(i =>
        {
            var l = byLease[i.LeaseId];
            return new InvoiceLeaseLinkDto(i.InvoiceId!.Value, l.Id, l.Number, l.PropertyId, props.GetValueOrDefault(l.PropertyId), unitNos.GetValueOrDefault(l.UnitId), i.PeriodFrom, i.PeriodTo);
        }).ToList();
    }
}
