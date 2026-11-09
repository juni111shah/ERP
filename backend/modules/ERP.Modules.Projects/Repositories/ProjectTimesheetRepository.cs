using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Features;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Repositories;

internal sealed class ProjectTimesheetRepository(IRepository<ProjectTimesheet> inner) : RepositoryBase<ProjectTimesheet>(inner), IProjectTimesheetRepository
{
    public Task<int> CountReadyToBillAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectTimesheet>(t => t.ProjectId == projectId && t.IsBillable && !t.IsBilled && !t.WrittenOff && t.ApprovalStatus == TimesheetStatus.Approved)).CountAsync(ct);

    public Task<List<ProjectTimesheet>> ListByIdsForUpdateAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
        => Tracked(new Specification<ProjectTimesheet>(t => ids.Contains(t.Id))).ToListAsync(ct);

    public Task<decimal> HoursOnDayOfUserAsync(DateOnly date, Guid? exceptId, Guid userId, CancellationToken ct)
        => OnDay(date, exceptId).Where(t => t.WorkerUserId == userId).SumDecimalAsync(t => t.Hours, ct);

    public Task<decimal> HoursOnDayOfNameAsync(DateOnly date, Guid? exceptId, string name, CancellationToken ct)
        => OnDay(date, exceptId).Where(t => t.WorkerUserId == null && t.WorkerName == name).SumDecimalAsync(t => t.Hours, ct);

    /// <summary>The entries of a day, leaving out <paramref name="exceptId"/>.</summary>
    private IQueryable<ProjectTimesheet> OnDay(DateOnly date, Guid? exceptId)
        => Query(new Specification<ProjectTimesheet>(t => t.Date == date && (exceptId == null || t.Id != exceptId)));

    public Task<List<ProjectTimesheet>> ListReadyToBillAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectTimesheet>(x => x.ProjectId == projectId && x.IsBillable && !x.IsBilled && !x.WrittenOff && x.ApprovalStatus == TimesheetStatus.Approved)).OrderBy(x => x.Date).ToListAsync(ct);

    public Task<List<ProjectTimesheet>> ListReadyToBillForUpdateAsync(Guid projectId, CancellationToken ct)
        => Tracked(new Specification<ProjectTimesheet>(x => x.ProjectId == projectId && x.IsBillable && !x.IsBilled && !x.WrittenOff && x.ApprovalStatus == TimesheetStatus.Approved)).ToListAsync(ct);

    public Task<List<ProjectTimesheet>> ListChosenReadyToBillForUpdateAsync(Guid projectId, IReadOnlyList<Guid>? onlyIds, CancellationToken ct)
    {
        var tsQuery = Tracked(new Specification<ProjectTimesheet>(x => x.ProjectId == projectId && x.IsBillable && !x.IsBilled && !x.WrittenOff && x.ApprovalStatus == TimesheetStatus.Approved));
        if (onlyIds is not null) { var ids = onlyIds.ToHashSet(); tsQuery = tsQuery.Where(x => ids.Contains(x.Id)); }
        return tsQuery.OrderBy(x => x.Date).ToListAsync(ct);
    }

    public Task<List<UnbilledTimeRow>> ListUnbilledAsync(CancellationToken ct)
        => Query(new Specification<ProjectTimesheet>(x => x.IsBillable && !x.IsBilled && !x.WrittenOff)).Select(x => new UnbilledTimeRow(x.ProjectId, x.Hours, x.TotalAmount, x.ApprovalStatus)).ToListAsync(ct);

    public Task<List<ProjectTimesheet>> ListUnpostedCostForUpdateAsync(Guid projectId, CancellationToken ct)
        => Tracked(new Specification<ProjectTimesheet>(t => t.ProjectId == projectId && t.CostJournalId == null && t.CostAmount > 0 && t.ApprovalStatus == TimesheetStatus.Approved && !t.WrittenOff)).ToListAsync(ct);

    public Task<List<ProjectTimesheet>> ListOfProjectAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectTimesheet>(x => x.ProjectId == projectId)).ToListAsync(ct);

    public Task<List<ProjectTimesheet>> ListNotRejectedOfProjectAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectTimesheet>(x => x.ProjectId == projectId && x.ApprovalStatus != TimesheetStatus.Rejected)).ToListAsync(ct);

    public Task<List<ProjectTimesheet>> ListNotRejectedOfProjectsAsync(List<Guid> projectIds, CancellationToken ct)
        => Query(new Specification<ProjectTimesheet>(t => projectIds.Contains(t.ProjectId) && t.ApprovalStatus != TimesheetStatus.Rejected)).ToListAsync(ct);

    public Task<List<ProjectTimesheet>> ListNotRejectedOfProjectsBetweenAsync(List<Guid> projectIds, DateOnly? from, DateOnly? to, CancellationToken ct)
        => Query(new Specification<ProjectTimesheet>(t => projectIds.Contains(t.ProjectId) && t.ApprovalStatus != TimesheetStatus.Rejected && (from == null || t.Date >= from) && (to == null || t.Date <= to))).ToListAsync(ct);

    public Task<List<ProjectTimesheet>> ListNotRejectedBetweenAsync(DateOnly from, DateOnly to, CancellationToken ct)
        => Query(new Specification<ProjectTimesheet>(t => t.Date >= from && t.Date <= to && t.ApprovalStatus != TimesheetStatus.Rejected)).ToListAsync(ct);

    public Task<List<ProjectTimesheet>> ListNotRejectedBetweenAsync(DateOnly from, DateOnly to, Guid? projectId, CancellationToken ct)
        => Query(new Specification<ProjectTimesheet>(t => t.Date >= from && t.Date <= to && t.ApprovalStatus != TimesheetStatus.Rejected && (projectId == null || t.ProjectId == projectId))).ToListAsync(ct);

    public Task<List<ProjectTimesheet>> ListReadyToBillOfProjectsAsync(List<Guid> projectIds, CancellationToken ct)
        => Query(new Specification<ProjectTimesheet>(t => projectIds.Contains(t.ProjectId) && t.IsBillable && !t.IsBilled && !t.WrittenOff && t.ApprovalStatus == TimesheetStatus.Approved)).ToListAsync(ct);
}
