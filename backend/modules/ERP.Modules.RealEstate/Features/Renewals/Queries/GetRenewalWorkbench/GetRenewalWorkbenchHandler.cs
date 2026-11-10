using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetRenewalWorkbenchHandler(ILeaseRepository leases, IRealEstatePartyRepository parties, IPropertyRepository properties, IUnitRepository units)
    : IQueryHandler<GetRenewalWorkbenchQuery, RenewalWorkbenchDto>
{
    public async Task<RenewalWorkbenchDto> Handle(GetRenewalWorkbenchQuery q, CancellationToken ct)
    {
        // 1. The occupying leases ending within the window and their live renewals.
        var horizon = q.AsOf.AddDays(q.WithinDays);
        var ending = await leases.ListOccupyingEndingByAsync(horizon, ct);
        var ids = ending.Select(l => l.Id).ToList();
        var renewals = await leases.ListLiveRenewalsOfAsync(ids, ct);

        // 2. Tenant and property names, unit numbers and asking rents.
        var partyIds = ending.Select(l => l.LesseePartyId).Distinct().ToList();
        var propertyIds = ending.Select(l => l.PropertyId).Distinct().ToList();
        var unitIds = ending.Select(l => l.UnitId).Distinct().ToList();
        var tenants = await parties.NamesAsync(partyIds, ct);
        var props = await properties.NamesAsync(propertyIds, ct);
        var unitMap = await units.MapNumberAndAskingRentAsync(unitIds, ct);

        // 3. One row per lease, with its renewal state and the market variance.
        var rows = ending.Select(l =>
        {
            var renewal = renewals.FirstOrDefault(r => r.Of == l.Id);
            var state = renewal is null ? RenewalState.Due : renewal.Status == LeaseStatus.Draft ? RenewalState.DraftRenewal : RenewalState.Renewed;
            var unit = unitMap.GetValueOrDefault(l.UnitId);
            var market = unit?.AskingRent ?? 0m;
            return new RenewalRow(l.Id, l.Number, tenants.GetValueOrDefault(l.LesseePartyId, ""), props.GetValueOrDefault(l.PropertyId, ""), unit?.UnitNumber ?? "", l.EndDate, l.EndDate.DayNumber - q.AsOf.DayNumber,
                l.AnnualRent, market, market > 0 && l.AnnualRent > 0 ? Math.Round((market - l.AnnualRent) / l.AnnualRent * 100, 1) : null, l.CurrencyCode, state, renewal?.Id);
        }).ToList();
        return new(q.AsOf, q.WithinDays, rows, rows.Count(r => r.State == RenewalState.Due), rows.Count(r => r.State == RenewalState.DraftRenewal), rows.Count(r => r.State == RenewalState.Renewed));
    }
}
