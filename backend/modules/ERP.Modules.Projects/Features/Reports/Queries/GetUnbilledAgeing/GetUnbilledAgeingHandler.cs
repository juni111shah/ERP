using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class GetUnbilledAgeingHandler(
    IProjectRepository projects,
    IProjectTimesheetRepository timesheets,
    IProjectExpenseRepository expenses,
    IProjectMilestoneRepository milestones,
    ProjectAccess access)
    : IQueryHandler<GetUnbilledAgeingQuery, IReadOnlyList<AgeingRow>>
{
    public async Task<IReadOnlyList<AgeingRow>> Handle(GetUnbilledAgeingQuery q, CancellationToken ct)
    {
        // 1. The billable projects the caller may see, with their work ready to bill.
        var asOf = q.AsOf ?? DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        var visible = await access.VisibleIdsAsync(ct);
        var list = (await projects.ListBillableNonTemplatesAsync(ct)).Where(p => visible == null || visible.Contains(p.Id)).ToList();
        var ids = list.Select(p => p.Id).ToList();
        var ts = (await timesheets.ListReadyToBillOfProjectsAsync(ids, ct)).ToLookup(t => t.ProjectId);
        var ex = (await expenses.ListReadyToBillOfProjectsAsync(ids, ct)).ToLookup(e => e.ProjectId);
        var ms = (await milestones.ListAchievedOfProjectsAsync(ids, ct)).ToLookup(m => m.ProjectId);

        // 2. Age it; projects with nothing to bill are left out.
        var rows = new List<AgeingRow>();
        foreach (var p in list.OrderBy(p => p.Number))
        {
            var b = new decimal[4];
            if (p.BillingType != ProjectBillingType.FixedFee) foreach (var t in ts[p.Id]) b[ReportMath.Bucket(t.Date, asOf)] += t.TotalAmount;
            foreach (var e in ex[p.Id]) b[ReportMath.Bucket(e.Date, asOf)] += BillingLines.Charge(e);
            foreach (var m in ms[p.Id]) b[ReportMath.Bucket(m.AchievedDate ?? m.DueDate, asOf)] += m.Amount;
            var total = b.Sum();
            if (total > 0) rows.Add(new AgeingRow(p.Id, p.Number, p.Name, b[0], b[1], b[2], b[3], total));
        }
        return rows;
    }
}
