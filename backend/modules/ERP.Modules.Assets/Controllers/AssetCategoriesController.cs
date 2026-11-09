using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Assets.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Assets.Controllers;

/// <summary>Asset categories: depreciation defaults and accounts.</summary>
[ModuleGate(AssetsModule.Key)]
[Route("api/v1/m/assets/categories")]
public sealed class AssetCategoriesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.CategoriesRead)]
    public Task<PagedResult<AssetCategoryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, AssetCategoryDto.Projection, Permissions.CategoriesRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.CategoriesRead)]
    public Task<AssetCategoryDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetAssetCategoryQuery(id), ct);

    [HttpPost]
    [RequirePermission(Permissions.CategoriesManage)]
    public async Task<IActionResult> Create(AssetCategoryInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateAssetCategoryCommand(input), ct);
        return Created($"/api/v1/m/assets/categories/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.CategoriesManage)]
    public Task<AssetCategoryDto> Update(Guid id, AssetCategoryInput input, CancellationToken ct)
        => Mediator.Send(new UpdateAssetCategoryCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.CategoriesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteAssetCategoryCommand(id), ct);
        return NoContent();
    }
}
