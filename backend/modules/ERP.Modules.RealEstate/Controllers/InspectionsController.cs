using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>Move-in, move-out and routine inspections with their checklists.</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/inspections")]
public sealed class InspectionsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.InspectionsRead)]
    public Task<PagedResult<InspectionRow>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, InspectionRow.Projection, P.InspectionsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.InspectionsRead)]
    public Task<InspectionDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetInspectionQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.InspectionsManage)]
    public async Task<IActionResult> Create(InspectionInput input, CancellationToken ct)
    {
        var c = await Mediator.Send(new CreateInspectionCommand(input), ct);
        return Created($"/api/v1/m/realestate/inspections/{c.Id}", c);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.InspectionsManage)]
    public Task<InspectionDto> Update(Guid id, InspectionInput input, CancellationToken ct)
        => Mediator.Send(new UpdateInspectionCommand(id, input), ct);

    [HttpPost("{id:guid}/complete")]
    [RequirePermission(P.InspectionsManage)]
    public Task<InspectionDto> Complete(Guid id, CompleteInspectionRequest body, CancellationToken ct)
        => Mediator.Send(new CompleteInspectionCommand(id, body.RaiseWorkOrders), ct);

    public sealed record CompleteInspectionRequest(bool RaiseWorkOrders = false);
}
