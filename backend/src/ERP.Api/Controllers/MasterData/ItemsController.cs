using ERP.Application.Catalog;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.MasterData;

/// <summary>Items: products and services.</summary>
[Route("api/v1/items")]
public sealed class ItemsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.ItemsRead)]
    public Task<PagedResult<ItemDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList<Item, ItemDto>(query, "item", ItemDto.From, CorePermissions.ItemsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(CorePermissions.ItemsRead)]
    public Task<ItemDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetItemByIdQuery(id), ct);

    [HttpPut("{id:guid}")]
    [RequirePermission(CorePermissions.ItemsWrite)]
    public Task<ItemDto> Update(Guid id, ItemInput input, CancellationToken ct)
        => Mediator.Send(new UpdateItemCommand(id, input), ct);

    [HttpPost]
    [RequirePermission(CorePermissions.ItemsWrite)]
    public async Task<IActionResult> Create(ItemInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateItemCommand(input), ct);
        return Created($"/api/v1/items/{created.Id}", created);
    }
}
