using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class ListLeasesHandler(
    IReadRepository<Lease> leases, IUnitRepository units, IPropertyRepository properties, IRealEstatePartyRepository parties, LeaseDtoFactory factory)
    : IQueryHandler<ListLeasesQuery, PagedResult<LeaseListItem>>
{
    public async Task<PagedResult<LeaseListItem>> Handle(ListLeasesQuery q, CancellationToken ct)
    {
        // 1. The page of leases.
        var page = await leases.PageAsync(q, ct: ct);
        var unitIds = page.Items.Select(l => l.UnitId).Distinct().ToList();
        var propertyIds = page.Items.Select(l => l.PropertyId).Distinct().ToList();
        var tenantIds = page.Items.Select(l => l.LesseePartyId).Distinct().ToList();

        // 2. The unit numbers, property names and tenant names they show.
        var unitNumbers = await units.NumbersAsync(unitIds, ct);
        var propertyNames = await properties.NamesAsync(propertyIds, ct);
        var tenantNames = await parties.NamesAsync(tenantIds, ct);

        // 3. One row per lease.
        return page.Map(l => new LeaseListItem(
            l.Id, l.Number, l.UnitId, unitNumbers.GetValueOrDefault(l.UnitId, ""), l.PropertyId, propertyNames.GetValueOrDefault(l.PropertyId, ""), l.LesseePartyId,
            tenantNames.GetValueOrDefault(l.LesseePartyId, ""), l.StartDate, l.EndDate, factory.DaysToExpiry(l), l.AnnualRent, l.PaymentFrequency,
            l.SecurityDepositAmount, l.Status));
    }
}
