using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetRentRollHandler(
    ILeaseRepository leases, IUnitRepository units, IUnitTypeRepository unitTypes, IPropertyRepository properties, IRealEstatePartyRepository parties,
    IRealEstateChequeRepository instruments, IRealEstateInvoiceRepository invoices, IRentInstalmentRepository instalments)
    : IQueryHandler<GetRentRollQuery, RentRollDto>
{
    public async Task<RentRollDto> Handle(GetRentRollQuery q, CancellationToken ct)
    {
        // 1. The occupying leases in scope, with their units, types, properties and tenants.
        var entity = q.LegalEntityId; var propertyIds = q.PropertyIds ?? [];
        var active = await leases.ListRentRollLeasesAsync(entity, propertyIds, ct);
        var unitIds = active.Select(l => l.UnitId).ToList(); var propIds = active.Select(l => l.PropertyId).Distinct().ToList(); var partyIds = active.Select(l => l.LesseePartyId).Distinct().ToList();
        var unitMap = await units.MapRentRollUnitsAsync(unitIds, ct);
        var typeIds = unitMap.Values.Select(u => u.UnitTypeId).Distinct().ToList();
        var typeNames = await unitTypes.NamesAsync(typeIds, ct);
        var propNames = await properties.NamesAsync(propIds, ct);
        var tenants = await parties.NamesAsync(partyIds, ct);

        // 2. The tenants' cheques in custody and open invoices.
        var pdcs = await instruments.InCustodyTotalsOfPartiesAsync(partyIds, ct);
        var open = await invoices.ListOpenOfPartiesAsync(partyIds, ct);

        // 3. What is owed on each lease; anything else the tenant owes (other invoices, a lease that ended) is shown once, on the tenant's first row
        var billedInstalments = open.Where(i => i.SourceId != null && i.SourceType is RentInvoicer.SourceType or "realestate.termination").Select(i => i.SourceId!.Value).Distinct().ToList();
        var instalmentLease = await instalments.MapLeaseIdsAsync(billedInstalments, ct);
        var otherLeaseIds = instalmentLease.Values.Distinct().ToList();
        var otherLeases = await leases.MapPlacementsAsync(otherLeaseIds, ct);
        var listed = active.Select(l => l.Id).ToHashSet();
        var owedOnLease = new Dictionary<Guid, decimal>(); var owedOther = new Dictionary<Guid, decimal>();
        foreach (var i in open)
        {
            var amount = (i.IsCreditNote ? -1 : 1) * (i.Total - i.Paid);
            Guid? leaseId = i.SourceId is { } source && instalmentLease.TryGetValue(source, out var found) ? found : null;
            if (leaseId is { } lid && listed.Contains(lid)) { owedOnLease[lid] = owedOnLease.GetValueOrDefault(lid) + amount; continue; }
            // rent of a lease the report was told to leave out stays out
            if (leaseId is { } hidden && otherLeases.TryGetValue(hidden, out var o) && ((propertyIds.Length > 0 && !propertyIds.Contains(o.PropertyId)) || (entity != null && o.LegalEntityId != entity))) continue;
            owedOther[i.PartyId] = owedOther.GetValueOrDefault(i.PartyId) + amount;
        }

        // 4. One row per lease.
        var rows = active.Select(l =>
        {
            var u = unitMap[l.UnitId];
            var vat = l.TaxCodeId is not null ? "override" : u.Usage == UnitUsage.Commercial ? "standard" : "exempt";
            return new RentRollRow(l.PropertyId, propNames.GetValueOrDefault(l.PropertyId, "?"), l.UnitId, u.UnitNumber, typeNames.GetValueOrDefault(u.UnitTypeId), u.AreaSqft, l.Id, l.Number, tenants.GetValueOrDefault(l.LesseePartyId, "?"),
                l.StartDate, l.EndDate, l.EndDate.DayNumber - q.AsOf.DayNumber, l.AnnualRent, u.AreaSqft > 0 ? Math.Round(l.AnnualRent / u.AreaSqft, 2) : 0, vat, 0m, owedOnLease.GetValueOrDefault(l.Id), l.LesseePartyId);
        }).OrderBy(r => r.Property).ThenBy(r => r.Unit).ToList();

        // 5. cheques and other balances belong to the tenant, not to a lease: one row carries them so a tenant with two leases is not counted twice
        var seen = new HashSet<Guid>();
        rows = rows.Select(r => seen.Add(r.TenantPartyId!.Value) ? r with { PdcsHeld = pdcs.GetValueOrDefault(r.TenantPartyId!.Value), Outstanding = r.Outstanding + owedOther.GetValueOrDefault(r.TenantPartyId!.Value) } : r).ToList();
        return new RentRollDto(q.AsOf, rows, rows.Sum(r => r.AnnualRent), rows.Sum(r => r.Outstanding));
    }
}
