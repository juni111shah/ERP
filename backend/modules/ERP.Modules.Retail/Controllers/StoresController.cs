using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Retail.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Retail.Controllers;

/// <summary>Retail stores.</summary>
[ModuleGate(RetailModule.Key)]
[Route("api/v1/m/retail/stores")]
public sealed class StoresController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.StoresRead)]
    public Task<PagedResult<RetailStoreDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, RetailStoreDto.Projection, Permissions.StoresRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.StoresRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var store = await Mediator.Send(new GetStoreByIdQuery(id), ct);
        return store is null ? NotFound() : Ok(store);
    }

    [HttpPost]
    [RequirePermission(Permissions.StoresManage)]
    public async Task<IActionResult> Create(CreateStoreCommand command, CancellationToken ct)
    {
        var created = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/retail/stores/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.StoresManage)]
    public Task<RetailStoreDto> Update(Guid id, UpdateStoreCommand command, CancellationToken ct)
        => Mediator.Send(command with { Id = id }, ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.StoresManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteStoreCommand(id), ct);
        return NoContent();
    }
}
