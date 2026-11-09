using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class WriteOffProjectHandler(
    IProjectRepository projects,
    IProjectTimesheetRepository timesheets,
    IProjectExpenseRepository expenses,
    IPostingService posting,
    ProjectAccess access,
    IUnitOfWork uow)
    : ICommandHandler<WriteOffProjectCommand, int>
{
    public Task<int> Handle(WriteOffProjectCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Load the project and everything that is ready to bill.
        var p = await projects.GetAsync(command.ProjectId, token);
        await access.EnsureAsync(p, token);
        var id = p.Id;
        var time = await timesheets.ListReadyToBillForUpdateAsync(id, token);
        var costs = await expenses.ListReadyToBillForUpdateAsync(id, token);

        // 2. Its cost leaves work in progress; write it all off and save.
        await WrittenOffWork.ReleaseAsync(posting, p, time, costs, token);
        foreach (var t in time) t.WrittenOff = true;
        foreach (var e in costs) e.WrittenOff = true;
        await timesheets.SaveChangesAsync(token);
        await expenses.SaveChangesAsync(token);
        return time.Count + costs.Count;
    }, ct);
}
