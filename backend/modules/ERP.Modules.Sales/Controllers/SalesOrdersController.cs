using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Sales.Controllers;

/// <summary>Sales orders, their deliveries and invoices.</summary>
[ModuleGate(SalesModule.Key)]
[Route("api/v1/m/sales/orders")]
public sealed class SalesOrdersController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.OrdersRead)]
    public Task<PagedResult<OrderSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, OrderSummaryDto.Projection, P.OrdersRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.OrdersRead)]
    public Task<OrderDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetOrderQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.OrdersManage)]
    public async Task<IActionResult> Create(OrderInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateOrderCommand(input), ct);
        return Created($"/api/v1/m/sales/orders/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.OrdersManage)]
    public Task<OrderDto> Update(Guid id, OrderInput input, CancellationToken ct)
        => Mediator.Send(new UpdateOrderCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.OrdersManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteOrderCommand(id), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [RequirePermission(P.OrdersManage)]
    public Task<OrderDto> ChangeStatus(Guid id, ChangeStatusRequest body, CancellationToken ct)
        => Mediator.Send(new ChangeOrderStatusCommand(id, body.Status), ct);

    [HttpPut("{id:guid}/fulfillment")]
    [RequirePermission(P.OrdersManage)]
    public Task<OrderDto> UpdateFulfillment(Guid id, UpdateFulfillmentRequest body, CancellationToken ct)
        => Mediator.Send(new UpdateOrderFulfillmentCommand(id, body.Lines, body.DeliveryDate, body.WarehouseId), ct);

    [HttpPost("{id:guid}/convert-to-invoice")]
    [RequirePermission(P.OrdersManage)]
    public Task<InvoiceDto> ConvertToInvoice(Guid id, CancellationToken ct)
        => Mediator.Send(new ConvertOrderToInvoiceCommand(id), ct);

    /// <summary>The status to move the order to.</summary>
    public sealed record ChangeStatusRequest(SalesOrderStatus Status);

    /// <summary>The cumulative delivered quantity per line, and optionally the delivery date and warehouse.</summary>
    public sealed record UpdateFulfillmentRequest(List<OrderFulfillmentLineInput> Lines, DateOnly? DeliveryDate = null, Guid? WarehouseId = null);
}
