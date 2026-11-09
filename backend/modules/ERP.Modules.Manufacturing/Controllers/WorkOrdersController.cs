using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Manufacturing.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Manufacturing.Controllers;

/// <summary>Production work orders.</summary>
[ModuleGate(ManufacturingModule.Key)]
[Route("api/v1/m/manufacturing/workorders")]
public sealed class WorkOrdersController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.WorkOrdersRead)]
    public Task<PagedResult<WorkOrderSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, WorkOrderSummaryDto.Projection, Permissions.WorkOrdersRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.WorkOrdersRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var workOrder = await Mediator.Send(new GetWorkOrderByIdQuery(id), ct);
        return workOrder is null ? NotFound() : Ok(workOrder);
    }

    [HttpPost]
    [RequirePermission(Permissions.WorkOrdersManage)]
    public async Task<IActionResult> Create(CreateWorkOrderCommand command, CancellationToken ct)
    {
        var created = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/manufacturing/workorders/{created.Id}", created);
    }

    [HttpPost("{id:guid}/release")]
    [RequirePermission(Permissions.WorkOrdersManage)]
    public Task<WorkOrderDto> Release(Guid id, ReleaseWorkOrderRequest? body, CancellationToken ct)
        => Mediator.Send(new ReleaseWorkOrderCommand(id, body?.IssueMaterials), ct);

    [HttpPost("{id:guid}/issue")]
    [RequirePermission(Permissions.WorkOrdersManage)]
    public Task<WorkOrderDto> IssueMaterials(Guid id, IssueWorkOrderMaterialsCommand command, CancellationToken ct)
        => Mediator.Send(command with { WorkOrderId = id }, ct);

    [HttpPost("{id:guid}/return-materials")]
    [RequirePermission(Permissions.WorkOrdersManage)]
    public Task<WorkOrderDto> ReturnMaterials(Guid id, ReturnWorkOrderMaterialsCommand command, CancellationToken ct)
        => Mediator.Send(command with { WorkOrderId = id }, ct);

    [HttpPut("{id:guid}/operations/{operationId:guid}/hours")]
    [RequirePermission(Permissions.WorkOrdersManage)]
    public Task<WorkOrderDto> RecordOperationHours(Guid id, Guid operationId, OperationHoursRequest body, CancellationToken ct)
        => Mediator.Send(new RecordOperationHoursCommand(id, operationId, body.ActualHours, body.Status), ct);

    [HttpPost("{id:guid}/complete")]
    [RequirePermission(Permissions.WorkOrdersManage)]
    public Task<WorkOrderDto> Complete(Guid id, CompleteWorkOrderCommand command, CancellationToken ct)
        => Mediator.Send(command with { Id = id }, ct);

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(Permissions.WorkOrdersManage)]
    public Task<Unit> Cancel(Guid id, CancelWorkOrderRequest body, CancellationToken ct)
        => Mediator.Send(new CancelWorkOrderCommand(id, body.Reason), ct);

    [HttpPost("{id:guid}/reverse")]
    [RequirePermission(Permissions.WorkOrdersReverse)]
    public Task<WorkOrderDto> Reverse(Guid id, ReverseWorkOrderRequest body, CancellationToken ct)
        => Mediator.Send(new ReverseWorkOrderCommand(id, body.Reason), ct);

    /// <summary>Optional: whether to issue the BOM on release (the setting decides when left out).</summary>
    public sealed record ReleaseWorkOrderRequest(bool? IssueMaterials);

    public sealed record OperationHoursRequest(decimal ActualHours, OperationStatus? Status);

    public sealed record CancelWorkOrderRequest(string? Reason);

    public sealed record ReverseWorkOrderRequest(string Reason);
}
