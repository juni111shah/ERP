using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>Change orders: changes to a project's fee, budget and hours, drafted and then decided.</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects/change-orders")]
public sealed class ChangeOrdersController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.ChangeOrdersRead)]
    public Task<IReadOnlyList<ChangeOrderDto>> List(Guid? projectId, CancellationToken ct)
        => Mediator.Send(new ListChangeOrdersQuery(projectId), ct);

    [HttpPost]
    [RequirePermission(P.ChangeOrdersManage)]
    public async Task<IActionResult> Create(ChangeOrderInput input, CancellationToken ct)
        => Created("/api/v1/m/projects/change-orders", await Mediator.Send(new CreateChangeOrderCommand(input), ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(P.ChangeOrdersManage)]
    public Task<ChangeOrderDto> Update(Guid id, ChangeOrderInput input, CancellationToken ct)
        => Mediator.Send(new UpdateChangeOrderCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.ChangeOrdersManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteChangeOrderCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/decide")]
    [RequirePermission(P.ChangeOrdersManage)]
    public Task<ChangeOrderDto> Decide(Guid id, DecideChangeRequest body, CancellationToken ct)
        => Mediator.Send(new DecideChangeOrderCommand(id, body.Approve, body.Note), ct);

    public sealed record DecideChangeRequest(bool Approve, string? Note);
}
