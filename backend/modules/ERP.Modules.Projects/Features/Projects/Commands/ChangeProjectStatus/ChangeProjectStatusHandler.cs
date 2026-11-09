using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class ChangeProjectStatusHandler(
    IProjectRepository projects,
    IProjectTimesheetRepository timesheets,
    IProjectExpenseRepository expenses,
    ProjectChecks checks,
    ProjectDtoBuilder dtos,
    ProjectAccess access,
    ERP.Application.Finance.IPostingService posting,
    IUnitOfWork uow)
    : ICommandHandler<ChangeProjectStatusCommand, ProjectDto>
{
    public Task<ProjectDto> Handle(ChangeProjectStatusCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. Load it and check the move; completing needs nothing left open or unbilled.
        var p = await projects.GetForUpdateAsync(command.Id, token);
        await access.EnsureAsync(p, token);
        ProjectsTransitions.Project.Ensure(p.Status, command.Status, isSystem: false);
        if (command.Status == ProjectStatus.Completed) await checks.EnsureCanCompleteAsync(p, token);

        // 2. Move it; a closed project releases what is left in work in progress.
        p.Status = command.Status;
        if (command.Status is ProjectStatus.Completed or ProjectStatus.Cancelled) await ProjectWip.ReleaseRemainingAsync(posting, timesheets, expenses, p, command.Status, token);

        // 3. Save and answer with the detail view.
        await projects.SaveChangesAsync(token);
        return await dtos.BuildAsync(p, token);
    }, ct);
}
