using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class SaveRateHandler(IProjectRateRepository rates, IProjectRepository projects, ProjectPeople people) : ICommandHandler<SaveRateCommand, RateDto>
{
    public async Task<RateDto> Handle(SaveRateCommand command, CancellationToken ct)
    {
        // 1. The project and the person must exist.
        var i = command.Input;
        if (i.ProjectId is { } pid) _ = await projects.GetAsync(pid, ct);
        string? userName = null;
        if (i.UserId is { } uid) userName = await people.RequireNameAsync(uid, "userId", ct);

        // 2. Only one rate per project, person and role.
        var role = string.IsNullOrWhiteSpace(i.Role) ? null : i.Role.Trim();
        var all = await rates.ListForProjectAndUserAsync(i.ProjectId, i.UserId, ct);
        if (all.Any(r => r.Id != command.Id && string.Equals(r.Role, role, StringComparison.OrdinalIgnoreCase))) throw new ConflictException("There is already a rate for that project, person and role.");

        // 3. Create or change it and save.
        ProjectRate row;
        if (command.Id is { } id) row = await rates.GetForUpdateAsync(id, ct); else { row = new ProjectRate(); rates.Add(row); }
        row.ProjectId = i.ProjectId; row.UserId = i.UserId; row.Role = role; row.BillRate = i.BillRate; row.CostRate = i.CostRate;
        await rates.SaveChangesAsync(ct);
        return RateDto.From(row, userName);
    }
}
