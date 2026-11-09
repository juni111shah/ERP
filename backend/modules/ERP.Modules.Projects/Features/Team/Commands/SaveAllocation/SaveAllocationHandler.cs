using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class SaveAllocationHandler(IProjectAllocationRepository allocations, IProjectRepository projects, ProjectPeople people, ProjectAccess access) : ICommandHandler<SaveAllocationCommand, AllocationDto>
{
    public async Task<AllocationDto> Handle(SaveAllocationCommand command, CancellationToken ct)
    {
        // 1. The project must be open and the person a workspace member.
        var i = command.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);
        ProjectRules.EnsureOpen(p);
        await access.EnsureAsync(p, ct);
        var name = await people.RequireNameAsync(i.UserId, "userId", ct);

        // 2. Create or load it, fill it in and save.
        ProjectAllocation a;
        if (command.Id is { } id) a = await allocations.GetForUpdateAsync(id, ct); else { a = new ProjectAllocation(); allocations.Add(a); }
        a.ProjectId = p.Id; a.UserId = i.UserId; a.UserName = name; a.FromDate = i.FromDate; a.ToDate = i.ToDate; a.HoursPerWeek = i.HoursPerWeek;
        await allocations.SaveChangesAsync(ct);
        return new AllocationDto(a.Id, a.ProjectId, a.UserId, a.UserName, a.FromDate, a.ToDate, a.HoursPerWeek);
    }
}
