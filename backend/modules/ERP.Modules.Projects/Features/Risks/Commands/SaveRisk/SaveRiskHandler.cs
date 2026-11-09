using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class SaveRiskHandler(IProjectRiskRepository risks, IProjectRepository projects, ProjectPeople people, ProjectAccess access) : ICommandHandler<SaveRiskCommand, RiskDto>
{
    public async Task<RiskDto> Handle(SaveRiskCommand command, CancellationToken ct)
    {
        // 1. The project must be one the caller may reach; an owner account gives the owner's name.
        var i = command.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);
        await access.EnsureAsync(p, ct);
        string? owner = i.OwnerName?.Trim();
        if (i.OwnerUserId is { } uid) owner = await people.RequireNameAsync(uid, "ownerUserId", ct);

        // 2. Create it, or load it (it stays on its project), then fill it in and save.
        ProjectRisk r;
        if (command.Id is { } id) { r = await risks.GetForUpdateAsync(id, ct); if (r.ProjectId != p.Id) throw new FieldValidationException("projectId", "A risk cannot move to another project."); }
        else { r = new ProjectRisk { ProjectId = p.Id }; risks.Add(r); }
        r.Kind = i.Kind; r.Title = i.Title.Trim(); r.Notes = i.Notes?.Trim(); r.Severity = i.Severity; r.Status = i.Status; r.OwnerUserId = i.OwnerUserId; r.OwnerName = owner; r.DueDate = i.DueDate;
        await risks.SaveChangesAsync(ct);
        return RiskDto.From(r);
    }
}
