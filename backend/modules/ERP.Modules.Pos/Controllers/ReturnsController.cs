using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Pos.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Pos.Controllers;

/// <summary>Counter returns and refunds.</summary>
[ModuleGate(PosModule.Key)]
[Route("api/v1/m/pos/returns")]
public sealed class ReturnsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.OrdersRead)]
    public Task<PagedResult<PosReturnSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, PosReturnSummaryDto.Projection, Permissions.OrdersRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.OrdersRead)]
    public Task<PosReturnDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetPosReturnByIdQuery(id), ct);

    [HttpPost]
    [RequirePermission(Permissions.OrdersRefund)]
    public async Task<IActionResult> Create(CreatePosReturnCommand command, CancellationToken ct)
    {
        var created = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/pos/returns/{created.Id}", created);
    }
}
