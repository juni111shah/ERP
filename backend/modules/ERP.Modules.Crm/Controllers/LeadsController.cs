using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Crm.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Crm.Controllers;

/// <summary>CRM leads and their conversion into opportunities.</summary>
[ModuleGate(CrmModule.Key)]
[Route("api/v1/m/crm/leads")]
public sealed class LeadsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.LeadsRead)]
    public Task<PagedResult<LeadSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, LeadSummaryDto.Projection, Permissions.LeadsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.LeadsRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var lead = await Mediator.Send(new GetLeadByIdQuery(id), ct);
        return lead is null ? NotFound() : Ok(lead);
    }

    [HttpPost]
    [RequirePermission(Permissions.LeadsManage)]
    public async Task<IActionResult> Create(CreateLeadCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/crm/leads/{result.Id}", result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.LeadsManage)]
    public async Task<IActionResult> Update(Guid id, UpdateLeadCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest();
        return Ok(await Mediator.Send(command, ct));
    }

    [HttpPost("{id:guid}/convert")]
    [RequirePermission(Permissions.LeadsManage)]
    public async Task<IActionResult> Convert(Guid id, ConvertLeadCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest();
        return Ok(await Mediator.Send(command, ct));
    }
}
