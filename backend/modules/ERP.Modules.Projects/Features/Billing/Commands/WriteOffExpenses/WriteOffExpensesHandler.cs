using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class WriteOffExpensesHandler(
    IProjectRepository projects,
    IProjectExpenseRepository expenses,
    IPostingService posting,
    ProjectAccess access,
    IUnitOfWork uow)
    : ICommandHandler<WriteOffExpensesCommand, int>
{
    public Task<int> Handle(WriteOffExpensesCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Every expense must exist and none may be billed.
        var rows = await expenses.ListByIdsForUpdateAsync(command.Ids, token);
        if (rows.Count != command.Ids.Distinct().Count()) throw new NotFoundException("One of the expenses was not found.");
        if (rows.Any(e => e.IsBilled)) throw new ConflictException("Billed expenses cannot be written off. Void the invoice first.");
        await access.EnsureProjectsAsync(rows.Select(e => e.ProjectId), token);

        // 2. Per project, their cost leaves work in progress; write them off and save.
        foreach (var group in rows.GroupBy(e => e.ProjectId)) await WrittenOffWork.ReleaseAsync(posting, await projects.GetAsync(group.Key, token), [], group.ToList(), token);
        foreach (var e in rows) e.WrittenOff = true;
        await expenses.SaveChangesAsync(token);
        return rows.Count;
    }, ct);
}
