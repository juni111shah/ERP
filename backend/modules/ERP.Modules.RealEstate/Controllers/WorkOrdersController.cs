using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>Work orders: maintenance jobs on properties and units, their lifecycle and cost basis.</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/work-orders")]
public sealed class WorkOrdersController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.WorkOrdersRead)]
    public Task<PagedResult<WorkOrderDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList<WorkOrder, WorkOrderDto>(query, Docs.WorkOrder, WorkOrderDto.From, P.WorkOrdersRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.WorkOrdersRead)]
    public Task<WorkOrderDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetWorkOrderQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.WorkOrdersCreate)]
    public async Task<IActionResult> Create(WorkOrderInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateWorkOrderCommand(input), ct);
        return Created($"/api/v1/m/realestate/work-orders/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.WorkOrdersEdit)]
    public Task<WorkOrderDto> Update(Guid id, WorkOrderInput input, CancellationToken ct)
        => Mediator.Send(new UpdateWorkOrderCommand(id, input), ct);

    [HttpPut("{id:guid}/cost-basis")]
    [RequirePermission(P.WorkOrdersEdit)]
    public Task<WorkOrderDto> SetCostBasis(Guid id, CostBasisInput input, CancellationToken ct)
        => Mediator.Send(new SetWorkOrderCostBasisCommand(id, input), ct);

    [HttpPost("{id:guid}/status")]
    [Authorize] // permission depends on the target status
    public Task<WorkOrderDto> ChangeStatus(Guid id, WorkOrderStatusRequest body, CancellationToken ct)
        => Mediator.Send(new ChangeWorkOrderStatusCommand(id, body.Status, body.ActualCost, body.ResolutionNotes), ct);

    public sealed record WorkOrderStatusRequest(WorkOrderStatus Status, decimal? ActualCost = null, string? ResolutionNotes = null);
}
