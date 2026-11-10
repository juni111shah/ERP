using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Services.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Services.Controllers;

/// <summary>Billing milestones.</summary>
[ModuleGate(ServicesModule.Key)]
[Route("api/v1/m/services/milestones")]
public sealed class MilestonesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.MilestonesRead)]
    public Task<PagedResult<MilestoneSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, MilestoneSummaryDto.Projection, Permissions.MilestonesRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.MilestonesRead)]
    public Task<MilestoneDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetMilestoneQuery(id), ct);

    [HttpPost]
    [RequirePermission(Permissions.MilestonesManage)]
    public async Task<IActionResult> Create(CreateMilestoneCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/services/milestones/{result.Id}", result);
    }

    [HttpPost("{id:guid}/status")]
    [RequirePermission(Permissions.MilestonesManage)]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateMilestoneStatusCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest();
        var result = await Mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/invoice")]
    [RequirePermission(Permissions.MilestonesManage)]
    public Task<InvoiceDto> Invoice(Guid id, CancellationToken ct)
        => Mediator.Send(new InvoiceMilestoneCommand(id), ct);
}
