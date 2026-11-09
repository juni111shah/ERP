using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Purchasing.Controllers;

/// <summary>Purchase requisitions.</summary>
[ModuleGate(PurchasingModule.Key)]
[Route("api/v1/m/purchasing/requisitions")]
public sealed class RequisitionsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.RequisitionsRead)]
    public Task<PagedResult<RequisitionSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, RequisitionSummaryDto.Projection, P.RequisitionsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.RequisitionsRead)]
    public Task<RequisitionDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetRequisitionQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.RequisitionsManage)]
    public async Task<IActionResult> Create(RequisitionInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateRequisitionCommand(input), ct);
        return Created($"/api/v1/m/purchasing/requisitions/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.RequisitionsManage)]
    public Task<RequisitionDto> Update(Guid id, RequisitionInput input, CancellationToken ct)
        => Mediator.Send(new UpdateRequisitionCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.RequisitionsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteRequisitionCommand(id), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [RequirePermission(P.RequisitionsManage)]
    public Task<RequisitionDto> ChangeStatus(Guid id, ChangeStatusRequest body, CancellationToken ct)
        => Mediator.Send(new ChangeRequisitionStatusCommand(id, body.Status), ct);

    [HttpPost("{id:guid}/convert-to-order")]
    [RequirePermission(P.OrdersManage)]
    public Task<PurchaseOrderDto> ConvertToOrder(Guid id, ConvertToOrderRequest body, CancellationToken ct)
        => Mediator.Send(new ConvertRequisitionToOrderCommand(id, body.VendorPartyId, body.VendorName), ct);

    /// <summary>The status to move the requisition to.</summary>
    public sealed record ChangeStatusRequest(PurchaseRequisitionStatus Status);

    /// <summary>The vendor of the new purchase order.</summary>
    public sealed record ConvertToOrderRequest(Guid? VendorPartyId, string VendorName);
}
