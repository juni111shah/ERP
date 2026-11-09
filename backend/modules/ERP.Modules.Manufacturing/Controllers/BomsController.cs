using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Manufacturing.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Manufacturing.Controllers;

/// <summary>Bills of materials (BOM).</summary>
[ModuleGate(ManufacturingModule.Key)]
[Route("api/v1/m/manufacturing/boms")]
public sealed class BomsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.BomsRead)]
    public Task<PagedResult<BillOfMaterialsSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, BillOfMaterialsSummaryDto.Projection, Permissions.BomsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.BomsRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var bom = await Mediator.Send(new GetBomByIdQuery(id), ct);
        return bom is null ? NotFound() : Ok(bom);
    }

    [HttpPost]
    [RequirePermission(Permissions.BomsManage)]
    public async Task<IActionResult> Create(CreateBomCommand command, CancellationToken ct)
    {
        var created = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/manufacturing/boms/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.BomsManage)]
    public Task<BillOfMaterialsDto> Update(Guid id, UpdateBomCommand command, CancellationToken ct)
        => Mediator.Send(command with { Id = id }, ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.BomsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteBomCommand(id), ct);
        return NoContent();
    }
}
