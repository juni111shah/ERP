using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Services.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Services.Controllers;

/// <summary>Client engagements and their time billing.</summary>
[ModuleGate(ServicesModule.Key)]
[Route("api/v1/m/services/engagements")]
public sealed class EngagementsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.EngagementsRead)]
    public Task<PagedResult<EngagementSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, EngagementSummaryDto.Projection, Permissions.EngagementsRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.EngagementsRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var engagement = await Mediator.Send(new GetEngagementByIdQuery(id), ct);
        return engagement is null ? NotFound() : Ok(engagement);
    }

    [HttpPost]
    [RequirePermission(Permissions.EngagementsManage)]
    public async Task<IActionResult> Create(CreateEngagementCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/services/engagements/{result.Id}", result);
    }

    [HttpPost("{id:guid}/invoice-time")]
    [RequirePermission(Permissions.EngagementsManage)]
    public Task<InvoiceDto> InvoiceTime(Guid id, EngagementTimeInvoiceRequest? body, CancellationToken ct)
        => Mediator.Send(new InvoiceEngagementTimeCommand(id, body?.TimeEntryIds, body?.From, body?.To, body?.InvoiceDate, body?.DueDate, body?.TaxCodeId, body?.Notes), ct);

    [HttpPost("{id:guid}/status")]
    [RequirePermission(Permissions.EngagementsManage)]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateEngagementStatusCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest();
        var result = await Mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>Optional: which time to invoice (ids or a date range; none means everything ready), the invoice dates, tax code and notes.</summary>
    public sealed record EngagementTimeInvoiceRequest(IReadOnlyList<Guid>? TimeEntryIds = null, DateOnly? From = null, DateOnly? To = null, DateOnly? InvoiceDate = null, DateOnly? DueDate = null, Guid? TaxCodeId = null, string? Notes = null);
}
