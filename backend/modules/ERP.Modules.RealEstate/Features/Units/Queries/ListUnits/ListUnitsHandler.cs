using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

internal sealed class ListUnitsHandler(
    IReadRepository<Unit> units, IPropertyRepository properties, IUnitTypeRepository unitTypes, ILeaseRepository leases, IRealEstatePartyRepository parties, IClock clock)
    : IQueryHandler<ListUnitsQuery, PagedResult<UnitListItem>>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<PagedResult<UnitListItem>> Handle(ListUnitsQuery q, CancellationToken ct)
    {
        // 1. The page of units.
        var page = await units.PageAsync(q, ct: ct);
        var unitIds = page.Items.Select(u => u.Id).ToList();
        var propertyIds = page.Items.Select(u => u.PropertyId).Distinct().ToList();
        var typeIds = page.Items.Select(u => u.UnitTypeId).Distinct().ToList();

        // 2. Their property and type names, and the occupying lease of each with its tenant.
        var propertyNames = await properties.NamesAsync(propertyIds, ct);
        var typeNames = await unitTypes.NamesAsync(typeIds, ct);
        var active = await leases.ListOccupyingOfUnitsAsync(unitIds, ct);
        var tenantIds = active.Select(l => l.LesseePartyId).Distinct().ToList();
        var tenantNames = await parties.NamesAsync(tenantIds, ct);
        var leaseByUnit = active.GroupBy(l => l.UnitId).ToDictionary(g => g.Key, g => g.First());

        // 3. One row per unit.
        return page.Map(u =>
        {
            leaseByUnit.TryGetValue(u.Id, out var lease);
            return new UnitListItem(
                u.Id, u.PropertyId, propertyNames.GetValueOrDefault(u.PropertyId, ""), u.UnitNumber, typeNames.GetValueOrDefault(u.UnitTypeId, ""), u.Usage, u.Floor,
                u.AreaSqft, u.AskingRent, u.Status, lease is null ? null : tenantNames.GetValueOrDefault(lease.LesseePartyId), lease?.AnnualRent, lease?.EndDate,
                u.Status == UnitStatus.Vacant && u.VacantSince is { } since ? Math.Max(0, Today.DayNumber - since.DayNumber) : null);
        });
    }
}
