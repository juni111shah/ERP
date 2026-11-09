using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Repositories;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetLeaseExpiriesHandler(ILeaseRepository leases, IClock clock) : IQueryHandler<GetLeaseExpiriesQuery, LeaseExpiriesDto>
{
    public async Task<LeaseExpiriesDto> Handle(GetLeaseExpiriesQuery q, CancellationToken ct)
    {
        // 1. The active leases' end dates and rents.
        var entity = q.LegalEntityId; var today = DateOnly.FromDateTime(clock.UtcNow);
        var active = await leases.ListActiveEndsAsync(entity, ct);

        // 2. Those past their end, then one row per month.
        var rows = new List<ExpiryRow>();
        var past = active.Where(l => l.EndDate < today).ToList();
        if (past.Count > 0) rows.Add(new("Past end date", null, past.Count, past.Sum(l => l.AnnualRent), true));
        var first = new DateOnly(today.Year, today.Month, 1);
        for (var i = 0; i < Math.Clamp(q.Months, 1, 36); i++)
        {
            var m = first.AddMonths(i);
            var inMonth = active.Where(l => l.EndDate >= today && l.EndDate.Year == m.Year && l.EndDate.Month == m.Month).ToList();
            rows.Add(new(m.ToString("MMM yyyy"), m, inMonth.Count, inMonth.Sum(l => l.AnnualRent), false));
        }
        return new LeaseExpiriesDto(rows, rows.Sum(r => r.Leases), rows.Sum(r => r.AnnualRentAtRisk));
    }
}
