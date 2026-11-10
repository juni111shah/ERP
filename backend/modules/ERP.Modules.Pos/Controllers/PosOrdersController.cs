using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Pos.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Pos.Controllers;

/// <summary>Counter sales and held tickets.</summary>
[ModuleGate(PosModule.Key)]
[Route("api/v1/m/pos/orders")]
public sealed class PosOrdersController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.OrdersRead)]
    public Task<PagedResult<PosOrderSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, PosOrderSummaryDto.Projection, Permissions.OrdersRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.OrdersRead)]
    public Task<PosOrderDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetPosOrderByIdQuery(id), ct);

    [HttpGet("by-number/{number}")]
    [RequirePermission(Permissions.OrdersRead)]
    public Task<PosOrderDto> GetByNumber(string number, CancellationToken ct)
        => Mediator.Send(new GetPosOrderByNumberQuery(number), ct);

    [HttpPost]
    [RequirePermission(Permissions.OrdersCreate)]
    public async Task<IActionResult> Create(CreatePosOrderCommand command, CancellationToken ct)
    {
        var created = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/pos/orders/{created.Id}", created);
    }

    [HttpPost("quote")]
    [RequirePermission(Permissions.OrdersCreate)]
    public Task<PosOrderQuoteDto> Quote(QuotePosOrderQuery query, CancellationToken ct)
        => Mediator.Send(query, ct);

    [HttpPost("{id:guid}/void")]
    [RequirePermission(Permissions.OrdersVoid)]
    public Task<PosOrderDto> Void(Guid id, VoidOrderRequest body, CancellationToken ct)
        => Mediator.Send(new VoidPosOrderCommand(id, body.Reason), ct);

    [HttpPost("park")]
    [RequirePermission(Permissions.OrdersCreate)]
    public async Task<IActionResult> Park(ParkPosOrderCommand command, CancellationToken ct)
    {
        var parked = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/pos/orders/{parked.Id}", parked);
    }

    [HttpDelete("{id:guid}/park")]
    [RequirePermission(Permissions.OrdersCreate)]
    public async Task<IActionResult> DiscardParked(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DiscardParkedOrderCommand(id), ct);
        return NoContent();
    }

    /// <summary>Why the sale is voided.</summary>
    public sealed record VoidOrderRequest(string Reason);
}
