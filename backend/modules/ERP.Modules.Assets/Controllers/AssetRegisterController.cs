using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Assets.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Assets.Controllers;

/// <summary>The asset register: assets, capitalisation, disposal and each asset's schedule.</summary>
[ModuleGate(AssetsModule.Key)]
[Route("api/v1/m/assets/register")]
public sealed class AssetRegisterController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.RegisterRead)]
    public Task<PagedResult<AssetDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, AssetDto.Projection, Permissions.RegisterRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.RegisterRead)]
    public Task<AssetDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetAssetQuery(id), ct);

    [HttpPost]
    [RequirePermission(Permissions.RegisterManage)]
    public async Task<IActionResult> Create(AssetInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateAssetCommand(input), ct);
        return Created($"/api/v1/m/assets/register/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.RegisterManage)]
    public Task<AssetDto> Update(Guid id, AssetInput input, CancellationToken ct)
        => Mediator.Send(new UpdateAssetCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.RegisterManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteAssetCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/capitalise")]
    [RequirePermission(Permissions.RegisterCapitalise)]
    public Task<AssetDto> Capitalise(Guid id, CapitaliseInput input, CancellationToken ct)
        => Mediator.Send(new CapitaliseAssetCommand(id, input), ct);

    [HttpPost("{id:guid}/dispose")]
    [RequirePermission(Permissions.RegisterDispose)]
    public Task<AssetDto> DisposeOf(Guid id, DisposeInput input, CancellationToken ct)
        => Mediator.Send(new DisposeAssetCommand(id, input), ct);

    [HttpGet("{id:guid}/schedule")]
    [RequirePermission(Permissions.ReportsRead)]
    public Task<AssetScheduleDto> Schedule(Guid id, CancellationToken ct)
        => Mediator.Send(new GetAssetScheduleQuery(id), ct);
}
