using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class GetLeaveBalancesHandler(IEmployeeRepository employees, ILeaveTypeRepository types, ILeaveBalanceRepository balances, ILeaveRequestRepository requests, LeavePolicyService policy)
    : IQueryHandler<GetLeaveBalancesQuery, IReadOnlyList<LeaveBalanceRow>>
{
    public async Task<IReadOnlyList<LeaveBalanceRow>> Handle(GetLeaveBalancesQuery query, CancellationToken ct)
    {
        // 1. The people on the payroll in the year, the active types, their balances and pending days.
        var q = query;
        var start = new DateOnly(q.Year, 1, 1); var end = new DateOnly(q.Year, 12, 31);
        var staff = await employees.ListPayableInAsync(q.LegalEntityId, q.EmployeeId, start, end, ct);
        var ids = staff.Select(s => s.Id).ToList();
        var kinds = await types.ListActiveByCodeAsync(ct);
        var rows = await balances.MapForYearAsync(q.Year, ids, ct);
        var pending = await requests.PendingDaysByEmployeeAndTypeAsync(start, end, ids, ct);

        // 2. One row per employee and type that has something tracked.
        var result = new List<LeaveBalanceRow>();
        var allTiers = await policy.TiersByTypeAsync(ct);
        foreach (var e in staff)
            foreach (var t in kinds)
            {
                rows.TryGetValue((e.Id, t.Id), out var b);
                var entitled = b?.Entitled ?? LeaveEntitlement.Entitlement(t.AnnualDays, allTiers.GetValueOrDefault(t.Id) ?? [], t.ProRataFirstYear, e.JoinDate, q.Year); var carried = b?.Carried ?? 0m; var taken = b?.Taken ?? 0m;
                var wait = pending.GetValueOrDefault((e.Id, t.Id));
                if (b is null && entitled == 0 && t.AnnualDays == 0 && wait == 0) continue; // nothing tracked for this type
                result.Add(new(e.Id, e.Number, e.FullName, t.Id, t.Code, t.Name, q.Year, entitled, carried, taken, wait, entitled + carried - taken));
            }
        return result;
    }
}
