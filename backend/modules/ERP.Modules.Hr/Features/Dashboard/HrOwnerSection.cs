using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

/// <summary>Staff numbers, payroll cost and review results for the owner. Pay needs the payroll permission, as everywhere else.</summary>
public sealed class HrOwnerSection(IReadRepository<Employee> employees, IReadRepository<PayrollRun> runs, IReadRepository<PerformanceReview> reviews, ICurrentUser user) : IOwnerDashboardSection
{
    public string ModuleKey => HrModule.Key;

    public async Task<IReadOnlyList<OwnerSection>> BuildAsync(OwnerScope scope, CancellationToken ct)
    {
        if (!user.HasPermission(P.EmployeesRead)) return [];
        var entity = scope.LegalEntityId;
        var cards = new List<OwnerCard>
        {
            new("headcount", "Employees", await employees.CountAsync(e => e.Status != EmployeeStatus.Terminated && (entity == null || e.LegalEntityId == entity), ct), "number", Link: "/hr/employees"),
        };
        if (user.HasPermission(P.PayrollRead))
        {
            var fromKey = scope.From.Year * 100 + scope.From.Month;
            var toKey = scope.To.Year * 100 + scope.To.Month;
            var cost = (await runs.Query().Where(r => r.Status != PayrollStatus.Draft && r.Status != PayrollStatus.Voided && r.Year * 100 + r.Month >= fromKey && r.Year * 100 + r.Month <= toKey && (entity == null || r.LegalEntityId == entity))
                .Select(r => r.GrossEarnings).ToListAsync(ct)).Sum();
            cards.Add(new("payroll-cost", "Payroll in period", cost, Link: "/hr/payroll"));
        }
        if (user.HasPermission(P.ReviewsRead))
        {
            var done = await reviews.Query().Where(r => r.Status == ReviewStatus.Acknowledged || r.Status == ReviewStatus.Submitted).Select(r => r.OverallRating).ToListAsync(ct);
            var scored = done.Where(s => s is not null).Select(s => s!.Value).ToList();
            cards.Add(new("review-score", "Average review score", scored.Count == 0 ? 0 : Math.Round((decimal)scored.Average(), 2), "number", $"{scored.Count} reviews", "/hr/reviews"));
        }
        return [new("employees", "Employee performance", cards)];
    }
}
