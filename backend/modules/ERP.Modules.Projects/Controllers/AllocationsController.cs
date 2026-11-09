using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>Resource allocations: the hours per week people are planned on projects.</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects/allocations")]
public sealed class AllocationsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.ProjectsRead)]
    public Task<IReadOnlyList<AllocationDto>> List(Guid? projectId, Guid? userId, CancellationToken ct)
        => Mediator.Send(new ListAllocationsQuery(projectId, userId), ct);

    [HttpPost]
    [RequirePermission(P.TeamManage)]
    public async Task<IActionResult> Create(AllocationInput input, CancellationToken ct)
        => Created("/api/v1/m/projects/allocations", await Mediator.Send(new SaveAllocationCommand(null, input), ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(P.TeamManage)]
    public Task<AllocationDto> Update(Guid id, AllocationInput input, CancellationToken ct)
        => Mediator.Send(new SaveAllocationCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.TeamManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteAllocationCommand(id), ct);
        return NoContent();
    }
}
