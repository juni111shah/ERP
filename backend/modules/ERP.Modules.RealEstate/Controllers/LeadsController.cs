using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>Brokerage leads, their pipeline and conversion to deals.</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/leads")]
public sealed class LeadsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.CrmRead)]
    public Task<PagedResult<LeadDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, LeadDto.Projection, P.CrmRead, ct);

    [HttpGet("pipeline")]
    [RequirePermission(P.CrmRead)]
    public Task<LeadPipelineDto> GetPipeline(Guid? agentPartyId, CancellationToken ct)
        => Mediator.Send(new GetLeadPipelineQuery(agentPartyId), ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.CrmRead)]
    public Task<LeadDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetLeadQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.CrmManage)]
    public async Task<IActionResult> Create(LeadInput i, CancellationToken ct)
    {
        var c = await Mediator.Send(new CreateLeadCommand(i), ct);
        return Created($"/api/v1/m/realestate/leads/{c.Id}", c);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.CrmManage)]
    public Task<LeadDto> Update(Guid id, LeadInput i, CancellationToken ct)
        => Mediator.Send(new UpdateLeadCommand(id, i), ct);

    [HttpPost("{id:guid}/stage")]
    [RequirePermission(P.CrmManage)]
    public Task<LeadDto> MoveStage(Guid id, LeadStageRequest b, CancellationToken ct)
        => Mediator.Send(new MoveLeadStageCommand(id, b.Stage, b.LostReason), ct);

    [HttpPost("{id:guid}/convert")]
    [RequirePermission(P.CrmManage)]
    public Task<LeadDto> Convert(Guid id, ConvertLeadInput? i, CancellationToken ct)
        => Mediator.Send(new ConvertLeadToDealCommand(id, i ?? new ConvertLeadInput()), ct);

    public sealed record LeadStageRequest(LeadStage Stage, string? LostReason = null);
}
