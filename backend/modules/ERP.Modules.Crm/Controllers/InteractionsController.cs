using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Crm.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Crm.Controllers;

/// <summary>Customer interactions (activity logs).</summary>
[ModuleGate(CrmModule.Key)]
[Route("api/v1/m/crm/interactions")]
public sealed class InteractionsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.InteractionsRead)]
    public Task<PagedResult<CustomerInteractionDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, CustomerInteractionDto.Projection, Permissions.InteractionsRead, ct);

    [HttpPost]
    [RequirePermission(Permissions.InteractionsManage)]
    public async Task<IActionResult> Log(LogInteractionCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/crm/interactions/{result.Id}", result);
    }

    [HttpPost("{id:guid}/resolve")]
    [RequirePermission(Permissions.InteractionsManage)]
    public Task<bool> Resolve(Guid id, CancellationToken ct)
        => Mediator.Send(new ResolveInteractionCommand(id), ct);

    [HttpGet("by-entity")]
    [RequirePermission(Permissions.InteractionsRead)]
    public Task<List<CustomerInteractionDto>> ByEntity(string kind, Guid id, CancellationToken ct)
        => Mediator.Send(new GetInteractionsByRelatedEntityQuery(kind, id), ct);
}
