using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Organizations;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class UpdateProjectHandler(
    IProjectRepository projects,
    IProjectTaskRepository tasks,
    IProjectTimesheetRepository timesheets,
    IProjectExpenseRepository expenses,
    ProjectChecks checks,
    ProjectDtoBuilder dtos,
    ProjectPeople people,
    ProjectAccess access,
    ILegalEntityGuard guard,
    ICustomFieldService fields,
    ERP.Application.Finance.IPostingService posting,
    IUnitOfWork uow)
    : ICommandHandler<UpdateProjectCommand, ProjectDto>
{
    public Task<ProjectDto> Handle(UpdateProjectCommand command, CancellationToken ct) => uow.InTransactionAsync(token => UpdateAsync(command, token), ct);

    private async Task<ProjectDto> UpdateAsync(UpdateProjectCommand cmd, CancellationToken ct)
    {
        // 1. Load it; a move to another legal entity must be to one the caller may use.
        var p = await projects.GetForUpdateAsync(cmd.Id, ct);
        await access.EnsureAsync(p, ct);
        var i = cmd.Input;
        if (i.LegalEntityId != p.LegalEntityId)
            await guard.RequireAsync(i.LegalEntityId, ct);

        // 2. The status move, the template flag, the number and the legal entity must all be allowed.
        var customerName = await checks.EnsureCustomerAsync(i.CustomerId, ct);
        if (i.Status != p.Status) { ProjectsTransitions.Project.Ensure(p.Status, i.Status, isSystem: false); if (i.Status == ProjectStatus.Completed) await checks.EnsureCanCompleteAsync(p, ct); }
        if (i.IsTemplate == true && !p.IsTemplate && await timesheets.AnyAsync(x => x.ProjectId == p.Id, ct)) throw new ConflictException("A project that has time logged cannot become a template.");
        var number = string.IsNullOrWhiteSpace(i.Number) ? p.Number : i.Number.Trim();
        if (number != p.Number && await projects.AnyAsync(x => x.Number == number && x.Id != p.Id, ct))
            throw new ConflictException($"Project number '{number}' is already in use.");

        if (i.LegalEntityId != p.LegalEntityId && (await timesheets.AnyAsync(x => x.ProjectId == p.Id, ct) || await tasks.AnyAsync(x => x.ProjectId == p.Id, ct)))
            throw new ConflictException("A project that already has tasks or time cannot move to another legal entity.");

        // 3. Apply the form; closing the project releases what is left in work in progress.
        p.LegalEntityId = i.LegalEntityId;
        p.Number = number;
        var closing = i.Status != p.Status && i.Status is ProjectStatus.Completed or ProjectStatus.Cancelled;
        ProjectMapping.Map(i, p, customerName, await people.NameAsync(i.ManagerUserId, ct));
        if (closing) await ProjectWip.ReleaseRemainingAsync(posting, timesheets, expenses, p, i.Status, ct);

        // 4. Custom fields, save and answer with the detail view.
        await fields.ApplyAsync(Docs.Project, p, i.CustomFields, isCreate: false, ct);
        await projects.SaveChangesAsync(ct);
        return await dtos.BuildAsync(p, ct);
    }
}
