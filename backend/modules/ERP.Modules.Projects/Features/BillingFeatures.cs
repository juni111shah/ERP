using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Projects.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

public sealed record ProjectBillingSummaryDto(
    Guid ProjectId,
    string ProjectNumber,
    string ProjectName,
    ProjectBillingType BillingType,
    decimal UnbilledHours,
    decimal UnbilledHoursAmount,
    int AchievedMilestonesCount,
    decimal AchievedMilestonesAmount,
    decimal TotalUnbilledAmount);

public sealed record BillProjectInput(
    Guid ProjectId,
    IReadOnlyList<Guid>? TimesheetIds = null,
    IReadOnlyList<Guid>? MilestoneIds = null);

public sealed record BillProjectResultDto(
    Guid ProjectId,
    int BilledTimesheetsCount,
    int BilledMilestonesCount,
    decimal TotalAmountBilled);

public sealed record GetBillingOverview() : IQuery<IReadOnlyList<ProjectBillingSummaryDto>>, IRequirePermission
{
    public string RequiredPermission => P.BillingRead;
}

public sealed record BillProject(BillProjectInput Input) : ICommand<BillProjectResultDto>, IRequirePermission
{
    public string RequiredPermission => P.BillingManage;
}

public sealed class BillProjectValidator : AbstractValidator<BillProject>
{
    public BillProjectValidator()
    {
        RuleFor(x => x.Input.ProjectId).NotEmpty();
    }
}

internal sealed class BillingHandlers(
    IRepository<Project> projects,
    IRepository<ProjectTimesheet> timesheets,
    IRepository<ProjectMilestone> milestones) :
    IRequestHandler<GetBillingOverview, IReadOnlyList<ProjectBillingSummaryDto>>,
    IRequestHandler<BillProject, BillProjectResultDto>
{
    public async Task<IReadOnlyList<ProjectBillingSummaryDto>> Handle(GetBillingOverview q, CancellationToken ct)
    {
        var allProjects = await projects.Query().OrderBy(p => p.Name).ToListAsync(ct);
        var unbilledTs = await timesheets.Query(new Specification<ProjectTimesheet>(x => x.IsBillable && !x.IsBilled)).ToListAsync(ct);
        var achievedMs = await milestones.Query(new Specification<ProjectMilestone>(x => x.Status == MilestoneStatus.Achieved)).ToListAsync(ct);

        var list = new List<ProjectBillingSummaryDto>();

        foreach (var p in allProjects)
        {
            var pTs = unbilledTs.Where(x => x.ProjectId == p.Id).ToList();
            var pMs = achievedMs.Where(x => x.ProjectId == p.Id).ToList();

            var hours = pTs.Sum(x => x.Hours);
            var hoursAmount = pTs.Sum(x => x.TotalAmount);
            var msCount = pMs.Count;
            var msAmount = pMs.Sum(x => x.Amount);
            var total = hoursAmount + msAmount;

            if (total > 0 || p.Status == ProjectStatus.Active)
            {
                list.Add(new ProjectBillingSummaryDto(
                    p.Id,
                    p.Number,
                    p.Name,
                    p.BillingType,
                    hours,
                    hoursAmount,
                    msCount,
                    msAmount,
                    total));
            }
        }

        return list;
    }

    public async Task<BillProjectResultDto> Handle(BillProject cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);

        var tsQuery = timesheets.Query(new Specification<ProjectTimesheet>(x => x.ProjectId == p.Id && x.IsBillable && !x.IsBilled));
        if (i.TimesheetIds != null && i.TimesheetIds.Count > 0)
        {
            var ids = i.TimesheetIds.ToHashSet();
            tsQuery = tsQuery.Where(x => ids.Contains(x.Id));
        }
        var tsToBill = await tsQuery.ToListAsync(ct);

        var msQuery = milestones.Query(new Specification<ProjectMilestone>(x => x.ProjectId == p.Id && x.Status == MilestoneStatus.Achieved));
        if (i.MilestoneIds != null && i.MilestoneIds.Count > 0)
        {
            var ids = i.MilestoneIds.ToHashSet();
            msQuery = msQuery.Where(x => ids.Contains(x.Id));
        }
        var msToBill = await msQuery.ToListAsync(ct);

        decimal totalAmount = 0m;
        foreach (var ts in tsToBill)
        {
            ts.IsBilled = true;
            totalAmount += ts.TotalAmount;
        }

        foreach (var ms in msToBill)
        {
            ms.Status = MilestoneStatus.Invoiced;
            totalAmount += ms.Amount;
        }

        await timesheets.SaveChangesAsync(ct);
        await milestones.SaveChangesAsync(ct);

        return new BillProjectResultDto(p.Id, tsToBill.Count, msToBill.Count, totalAmount);
    }
}
