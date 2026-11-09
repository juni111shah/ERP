using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Features;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Purchasing.Controllers;

/// <summary>Purchase orders and the bills raised from them.</summary>
[ModuleGate(PurchasingModule.Key)]
[Route("api/v1/m/purchasing/orders")]
public sealed class PurchaseOrdersController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.OrdersRead)]
    public Task<PagedResult<PurchaseOrderSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, PurchaseOrderSummaryDto.Projection, P.OrdersRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.OrdersRead)]
    public Task<PurchaseOrderDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetPurchaseOrderQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.OrdersManage)]
    public async Task<IActionResult> Create(PurchaseOrderInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreatePurchaseOrderCommand(input), ct);
        return Created($"/api/v1/m/purchasing/orders/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.OrdersManage)]
    public Task<PurchaseOrderDto> Update(Guid id, PurchaseOrderInput input, CancellationToken ct)
        => Mediator.Send(new UpdatePurchaseOrderCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.OrdersManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeletePurchaseOrderCommand(id), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [RequirePermission(P.OrdersManage)]
    public Task<PurchaseOrderDto> ChangeStatus(Guid id, ChangeStatusRequest body, CancellationToken ct)
        => Mediator.Send(new ChangePurchaseOrderStatusCommand(id, body.Status), ct);

    /// <summary>The body is optional and read by hand: an absent or non-JSON body means "bill everything billable".</summary>
    [HttpPost("{id:guid}/convert-to-bill")]
    [RequirePermission(P.OrdersManage)]
    public async Task<BillDto> ConvertToBill(Guid id, CancellationToken ct)
    {
        ConvertToBillRequest? body = null;
        if (Request.HasJsonContentType() && Request.ContentLength != 0)
        {
            body = await Request.ReadFromJsonAsync<ConvertToBillRequest>(cancellationToken: ct);
        }
        return await Mediator.Send(new ConvertPurchaseOrderToBillCommand(id, body?.UnitPriceOverrides, body?.Lines, body?.AllowUnreceived ?? false, body?.VendorReference), ct);
    }

    /// <summary>The status to move the order to.</summary>
    public sealed record ChangeStatusRequest(PurchaseOrderStatus Status);

    /// <summary>Optional: prices that differ from the order, the lines and quantities to bill, billing ahead of receipt and the vendor's reference.</summary>
    public sealed record ConvertToBillRequest(Dictionary<Guid, decimal>? UnitPriceOverrides = null, List<BillOrderLineInput>? Lines = null, bool AllowUnreceived = false, string? VendorReference = null);
}
