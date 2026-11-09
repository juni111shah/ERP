using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Purchasing.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Purchasing.Controllers;

/// <summary>Returns to vendors.</summary>
[ModuleGate(PurchasingModule.Key)]
[Route("api/v1/m/purchasing/returns")]
public sealed class PurchaseReturnsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.ReturnsRead)]
    public Task<PagedResult<PurchaseReturnSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, PurchaseReturnSummaryDto.Projection, P.ReturnsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.ReturnsRead)]
    public Task<PurchaseReturnDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetPurchaseReturnQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.ReturnsManage)]
    public async Task<IActionResult> Create(PurchaseReturnInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreatePurchaseReturnCommand(input), ct);
        return Created($"/api/v1/m/purchasing/returns/{created.Id}", created);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.ReturnsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeletePurchaseReturnCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/post")]
    [RequirePermission(P.ReturnsManage)]
    public Task<PurchaseReturnDto> Post(Guid id, CancellationToken ct)
        => Mediator.Send(new PostPurchaseReturnCommand(id), ct);

    [HttpPost("{id:guid}/void")]
    [RequirePermission(P.ReturnsManage)]
    public Task<PurchaseReturnDto> Void(Guid id, VoidReturnRequest body, CancellationToken ct)
        => Mediator.Send(new VoidPurchaseReturnCommand(id, body.Reason), ct);

    /// <summary>Why the return is voided.</summary>
    public sealed record VoidReturnRequest(string? Reason);
}
