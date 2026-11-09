using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Sales.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Sales.Controllers;

/// <summary>Returns from customers.</summary>
[ModuleGate(SalesModule.Key)]
[Route("api/v1/m/sales/returns")]
public sealed class SalesReturnsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.ReturnsRead)]
    public Task<PagedResult<SalesReturnSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, SalesReturnSummaryDto.Projection, P.ReturnsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.ReturnsRead)]
    public Task<SalesReturnDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetSalesReturnQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.ReturnsManage)]
    public async Task<IActionResult> Create(SalesReturnInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateSalesReturnCommand(input), ct);
        return Created($"/api/v1/m/sales/returns/{created.Id}", created);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.ReturnsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteSalesReturnCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/post")]
    [RequirePermission(P.ReturnsManage)]
    public Task<SalesReturnDto> Post(Guid id, CancellationToken ct)
        => Mediator.Send(new PostSalesReturnCommand(id), ct);

    [HttpPost("{id:guid}/void")]
    [RequirePermission(P.ReturnsManage)]
    public Task<SalesReturnDto> Void(Guid id, VoidReturnRequest body, CancellationToken ct)
        => Mediator.Send(new VoidSalesReturnCommand(id, body.Reason), ct);

    /// <summary>Why the return is voided.</summary>
    public sealed record VoidReturnRequest(string? Reason);
}
