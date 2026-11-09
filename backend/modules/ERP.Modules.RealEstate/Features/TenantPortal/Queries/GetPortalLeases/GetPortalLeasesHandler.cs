using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetPortalLeasesHandler(
    ICurrentUser user, ILeaseRepository leases, IPropertyRepository properties, IUnitRepository units, IRentInstalmentRepository instalments, IRealEstateInvoiceRepository invoices, IClock clock)
    : IQueryHandler<GetPortalLeasesQuery, IReadOnlyList<PortalLeaseDto>>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<IReadOnlyList<PortalLeaseDto>> Handle(GetPortalLeasesQuery q, CancellationToken ct)
    {
        // 1. The tenant's leases and their schedules, with what was paid on each invoice.
        var me = TenantPortalUser.Me(user);
        var mine = await leases.ListOfTenantForPortalAsync(me, ct);
        var ids = mine.Select(l => l.Id).ToList();
        var schedule = await instalments.ListNotCancelledOfLeasesByDueDateAsync(ids, ct);
        var invoiceIds = schedule.Where(i => i.InvoiceId != null).Select(i => i.InvoiceId!.Value).Distinct().ToList();
        var invoiceMap = await invoices.MapTotalAndPaidAsync(invoiceIds, ct);

        // 2. Property names and unit numbers.
        var propertyIds = mine.Select(l => l.PropertyId).Distinct().ToList();
        var unitIds = mine.Select(l => l.UnitId).Distinct().ToList();
        var propNames = await properties.NamesAsync(propertyIds, ct);
        var unitNames = await units.NumbersAsync(unitIds, ct);

        // 3. One lease each, its instalments with their state.
        string State(RentInstalment i)
        {
            if (i.InvoiceId is { } id && invoiceMap.TryGetValue(id, out var inv)) return inv.Total - inv.Paid <= 0 ? "paid" : i.DueDate < Today ? "overdue" : "invoiced";
            return "scheduled";
        }
        return mine.Select(l => new PortalLeaseDto(l.Id, l.Number, propNames.GetValueOrDefault(l.PropertyId, ""), unitNames.GetValueOrDefault(l.UnitId, ""), l.StartDate, l.EndDate, l.AnnualRent, l.CurrencyCode, l.Status, l.PaymentFrequency, l.SecurityDepositAmount,
            schedule.Where(i => i.LeaseId == l.Id).Select(i => new PortalInstalmentDto(i.Sequence, i.DueDate, i.Amount, State(i))).ToList())).ToList();
    }
}
