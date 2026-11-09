using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Organizations;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class CreateProjectHandler(
    IProjectRepository projects,
    ProjectChecks checks,
    ProjectDtoBuilder dtos,
    ProjectPeople people,
    ILegalEntityGuard guard,
    INumberingService numbers,
    ICustomFieldService fields)
    : ICommandHandler<CreateProjectCommand, ProjectDto>
{
    public async Task<ProjectDto> Handle(CreateProjectCommand command, CancellationToken ct)
    {
        // 1. The legal entity must be one the caller may use; a new project starts as Planning or Active.
        var i = command.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        if (i.Status is not (ProjectStatus.Planning or ProjectStatus.Active))
            throw new FieldValidationException("status", "A new project starts as Planning or Active.");

        // 2. Fill it in and number it; the number must be free.
        var customerName = await checks.EnsureCustomerAsync(i.CustomerId, ct);
        var p = new Project { LegalEntityId = entity.Id, TaxCodeId = i.TaxCodeId };
        ProjectMapping.Map(i, p, customerName, await people.NameAsync(i.ManagerUserId, ct));
        p.Number = string.IsNullOrWhiteSpace(i.Number)
            ? await numbers.NextAsync(Docs.Project, entity.Id, null, ct)
            : i.Number.Trim();

        if (await projects.AnyAsync(x => x.Number == p.Number, ct))
            throw new ConflictException($"Project number '{p.Number}' is already in use.");

        // 3. Custom fields, save and answer with the detail view.
        await fields.ApplyAsync(Docs.Project, p, i.CustomFields, isCreate: true, ct);
        projects.Add(p);
        await projects.SaveChangesAsync(ct);
        return await dtos.BuildAsync(p, ct);
    }
}
