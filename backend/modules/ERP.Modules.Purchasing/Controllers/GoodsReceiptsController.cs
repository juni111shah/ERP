using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Purchasing.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Purchasing.Controllers;

/// <summary>Goods receipts against purchase orders.</summary>
[ModuleGate(PurchasingModule.Key)]
[Route("api/v1/m/purchasing/receipts")]
public sealed class GoodsReceiptsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.ReceiptsRead)]
    public Task<PagedResult<GoodsReceiptSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, GoodsReceiptSummaryDto.Projection, P.ReceiptsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.ReceiptsRead)]
    public Task<GoodsReceiptDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetGoodsReceiptQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.ReceiptsManage)]
    public async Task<IActionResult> Create(GoodsReceiptInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateGoodsReceiptCommand(input), ct);
        return Created($"/api/v1/m/purchasing/receipts/{created.Id}", created);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.ReceiptsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteGoodsReceiptCommand(id), ct);
        return NoContent();
    }
}
