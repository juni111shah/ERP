using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Retail.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Retail.Controllers;

/// <summary>Retail price lists.</summary>
[ModuleGate(RetailModule.Key)]
[Route("api/v1/m/retail/pricelists")]
public sealed class PriceListsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.PriceListsRead)]
    public Task<PagedResult<RetailPriceListSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, RetailPriceListSummaryDto.Projection, Permissions.PriceListsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.PriceListsRead)]
    public Task<RetailPriceListDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetPriceListByIdQuery(id), ct);

    [HttpPost]
    [RequirePermission(Permissions.PriceListsManage)]
    public async Task<IActionResult> Create(CreatePriceListCommand command, CancellationToken ct)
    {
        var created = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/retail/pricelists/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.PriceListsManage)]
    public Task<RetailPriceListDto> Update(Guid id, UpdatePriceListCommand command, CancellationToken ct)
        => Mediator.Send(command with { Id = id }, ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.PriceListsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeletePriceListCommand(id), ct);
        return NoContent();
    }
}
