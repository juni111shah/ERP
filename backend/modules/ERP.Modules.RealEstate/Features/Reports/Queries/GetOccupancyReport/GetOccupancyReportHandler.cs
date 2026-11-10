using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetOccupancyReportHandler(IPropertyRepository properties, IUnitRepository units, ILeaseRepository leases) : IQueryHandler<GetOccupancyReportQuery, OccupancyDto>
{
    public async Task<OccupancyDto> Handle(GetOccupancyReportQuery q, CancellationToken ct)
    {
        // 1. The active properties, their units and contracted rent.
        var entity = q.LegalEntityId;
        var props = await properties.ListActiveUnitCountsAsync(entity, ct);
        var unitRows = await units.ListOccupancyRowsAsync(entity, ct);
        var rent = await leases.ContractedRentByPropertyAsync(entity, ct);

        // 2. One row per property, then the totals.
        var rows = props.Select(p =>
        {
            var mine = unitRows.Where(u => u.PropertyId == p.Id).ToList();
            var occupied = mine.Count(u => u.Status == UnitStatus.Occupied); var vacant = mine.Count(u => u.Status == UnitStatus.Vacant);
            return new OccupancyRow(p.Id, p.Name, p.LegalEntityId, p.TotalUnits, mine.Count, occupied, vacant, mine.Count - occupied - vacant, mine.Count == 0 ? 0 : Math.Round(100m * occupied / mine.Count, 1),
                rent.GetValueOrDefault(p.Id), mine.Where(u => u.Status == UnitStatus.Vacant).Sum(u => u.AskingRent));
        }).ToList();
        var registered = rows.Sum(r => r.Registered); var occ = rows.Sum(r => r.Occupied);
        return new OccupancyDto(rows, registered == 0 ? 0 : Math.Round(100m * occ / registered, 1), rows.Sum(r => r.ContractedRent), rows.Sum(r => r.VacantAskingRent), rows.Sum(r => r.Vacant));
    }
}
