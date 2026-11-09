using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Pos.Features;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Pos.Controllers;

/// <summary>Cash registers.</summary>
[ModuleGate(PosModule.Key)]
[Route("api/v1/m/pos/registers")]
public sealed class RegistersController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.RegistersRead)]
    public Task<PagedResult<PosRegisterDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, PosRegisterDto.Projection, Permissions.RegistersRead, ct);

    /// <summary>A missing register answers a bare 404 (the error envelope comes from the status-code pages).</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.RegistersRead)]
    public async Task<object> GetById(Guid id, CancellationToken ct)
    {
        var register = await Mediator.Send(new GetRegisterByIdQuery(id), ct);
        return register is null ? Results.NotFound() : register;
    }

    [HttpPost]
    [RequirePermission(Permissions.RegistersManage)]
    public async Task<IActionResult> Create(CreateRegisterCommand command, CancellationToken ct)
    {
        var created = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/pos/registers/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.RegistersManage)]
    public Task<PosRegisterDto> Update(Guid id, UpdateRegisterCommand command, CancellationToken ct)
        => Mediator.Send(command with { Id = id }, ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.RegistersManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteRegisterCommand(id), ct);
        return NoContent();
    }
}
