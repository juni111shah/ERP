using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Application.Finance;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

/// <summary>Headcount and monthly payroll cost on the overview.</summary>
public sealed class HrDashboard(IReadRepository<Employee> employees, IReadRepository<PayrollRun> runs, ICurrentUser user) : IDashboardContributor
{
    public string ModuleKey => HrModule.Key;

    public async Task<DashboardContribution> BuildAsync(DashboardScope scope, CancellationToken ct)
    {
        var entity = scope.LegalEntityId;
        var kpis = new List<KpiCard>();
        if (user.HasPermission(P.EmployeesRead))
        {
            var staff = await employees.Query(new Specification<Employee>(e => e.Status != EmployeeStatus.Terminated && (entity == null || e.LegalEntityId == entity)))
                .Select(e => new { e.BasicSalary, e.HousingAllowance, e.TransportAllowance, e.OtherAllowances }).ToListAsync(ct);
            kpis.Add(new("employees", "Employees", staff.Count, "number", $"gross payroll {staff.Sum(s => s.BasicSalary + s.HousingAllowance + s.TransportAllowance + s.OtherAllowances):N0} a month", 110, "/hr/employees"));
        }
        if (user.HasPermission(P.PayrollRead))
        {
            var open = await runs.CountAsync(r => (r.Status == PayrollStatus.Draft || r.Status == PayrollStatus.PendingApproval || r.Status == PayrollStatus.Posted) && (entity == null || r.LegalEntityId == entity), ct);
            kpis.Add(new("payroll-open", "Payroll runs open", open, "number", "not yet paid", 115, "/hr/payroll"));
        }
        return new DashboardContribution(kpis, [], []);
    }
}

/// <summary>Payroll run-rate: the average net pay of the last runs, expected on the same day each month until a run exists.</summary>
public sealed class PayrollForecastSource(IReadRepository<PayrollRun> runs) : ICashForecastSource
{
    public string Key => "payroll";
    public string Label => "Payroll";
    public string? ModuleKey => HrModule.Key;

    public async Task<IReadOnlyList<ForecastItem>> ItemsAsync(Guid? entityId, DateOnly asOf, DateOnly horizon, CancellationToken ct)
    {
        var all = await runs.Query(new Specification<PayrollRun>(r => entityId == null || r.LegalEntityId == entityId)).Select(r => new { r.LegalEntityId, r.Year, r.Month, r.Status, r.NetPay, r.PlannedPayDate }).ToListAsync(ct);
        var items = new List<ForecastItem>();
        foreach (var group in all.GroupBy(r => r.LegalEntityId))
        {
            var recent = group.Where(r => r.Status is PayrollStatus.Posted or PayrollStatus.Paid).OrderByDescending(r => r.Year * 12 + r.Month).Take(3).ToList();
            if (recent.Count == 0) continue;
            var rate = recent.Average(r => r.NetPay);
            var day = Math.Min(recent[0].PlannedPayDate?.Day ?? 28, 28);
            var exists = group.Where(r => r.Status != PayrollStatus.Paid).ToDictionary(r => (r.Year, r.Month), r => r);
            for (var m = new DateOnly(asOf.Year, asOf.Month, 1); m <= horizon; m = m.AddMonths(1))
            {
                if (group.Any(r => r.Year == m.Year && r.Month == m.Month && r.Status == PayrollStatus.Paid)) continue; // already paid out
                var when = new DateOnly(m.Year, m.Month, day);
                if (when < asOf || when > horizon) continue;
                var run = exists.GetValueOrDefault((m.Year, m.Month));
                items.Add(new ForecastItem(run?.PlannedPayDate ?? when, -(run is { Status: PayrollStatus.Posted } ? run.NetPay : rate)));
            }
        }
        return items;
    }
}
