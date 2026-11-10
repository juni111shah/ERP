using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Crm.Features;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Crm.Controllers;

/// <summary>The CRM sales pipeline: opportunities, their stages and closing.</summary>
[ModuleGate(CrmModule.Key)]
[Route("api/v1/m/crm/opportunities")]
public sealed class OpportunitiesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.OpportunitiesRead)]
    public Task<PagedResult<OpportunitySummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, OpportunitySummaryDto.Projection, Permissions.OpportunitiesRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.OpportunitiesRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var opp = await Mediator.Send(new GetOpportunityByIdQuery(id), ct);
        return opp is null ? EmptyError(StatusCodes.Status404NotFound) : Ok(opp);
    }

    [HttpPost]
    [RequirePermission(Permissions.OpportunitiesManage)]
    public async Task<IActionResult> Create(CreateOpportunityCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/crm/opportunities/{result.Id}", result);
    }

    [HttpPost("{id:guid}/stage")]
    [RequirePermission(Permissions.OpportunitiesManage)]
    public async Task<IActionResult> UpdateStage(Guid id, UpdateOpportunityStageCommand command, CancellationToken ct)
    {
        if (id != command.Id) return EmptyError(StatusCodes.Status400BadRequest);
        return Ok(await Mediator.Send(command, ct));
    }

    [HttpPost("{id:guid}/close")]
    [RequirePermission(Permissions.OpportunitiesManage)]
    public async Task<IActionResult> Close(Guid id, CloseOpportunityCommand command, CancellationToken ct)
    {
        if (id != command.Id) return EmptyError(StatusCodes.Status400BadRequest);
        return Ok(await Mediator.Send(command, ct));
    }

    [HttpGet("pipeline-summary")]
    [RequirePermission(Permissions.OpportunitiesRead)]
    public Task<List<PipelineStageSummaryDto>> PipelineSummary(Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetPipelineSummaryQuery(legalEntityId), ct);

    /// <summary>A bare 404/400 with no title or detail, as the minimal endpoints answered (the envelope fills in the defaults).</summary>
    private static ObjectResult EmptyError(int status) => new(new ProblemDetails { Status = status }) { StatusCode = status };
}
