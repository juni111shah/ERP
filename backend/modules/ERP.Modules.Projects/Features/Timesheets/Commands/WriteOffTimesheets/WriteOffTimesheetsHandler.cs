using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class WriteOffTimesheetsHandler(
    IProjectTimesheetRepository timesheets,
    IProjectRepository projects,
    ProjectAccess access,
    ERP.Application.Finance.IPostingService posting,
    IUnitOfWork uow)
    : ICommandHandler<WriteOffTimesheetsCommand, int>
{
    public Task<int> Handle(WriteOffTimesheetsCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Every entry must exist and none may be billed.
        var rows = await timesheets.ListByIdsForUpdateAsync(command.Ids, token);
        if (rows.Count != command.Ids.Distinct().Count()) throw new NotFoundException("One of the entries was not found.");
        if (rows.Any(t => t.IsBilled)) throw new ConflictException("Billed time cannot be written off. Void the invoice first.");
        await access.EnsureProjectsAsync(rows.Select(t => t.ProjectId), token);

        // 2. Per project, cost already in work in progress goes to cost of sales.
        foreach (var group in rows.GroupBy(t => t.ProjectId))
        {
            var posted = group.Where(t => t.CostJournalId != null && t.WipReleaseJournalId == null).ToList();
            var p = await projects.GetAsync(group.Key, token);
            var journal = await ProjectWip.ReleaseAsync(posting, p, posted.Sum(t => t.CostAmount), p.Number, $"Project {p.Number} work written off, work in progress to cost of sales", token);
            if (journal is not null) foreach (var t in posted) t.WipReleaseJournalId = journal.Id;
        }

        // 3. Write them off and save.
        foreach (var t in rows) t.WrittenOff = true;
        await timesheets.SaveChangesAsync(token);
        return rows.Count;
    }, ct);
}
