using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Services.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Services.Controllers;

/// <summary>Retainer contracts and their period invoices.</summary>
[ModuleGate(ServicesModule.Key)]
[Route("api/v1/m/services/retainers")]
public sealed class RetainersController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.RetainersRead)]
    public Task<PagedResult<RetainerSummaryDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, RetainerSummaryDto.Projection, Permissions.RetainersRead, ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.RetainersRead)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var retainer = await Mediator.Send(new GetRetainerByIdQuery(id), ct);
        return retainer is null ? NotFound() : Ok(retainer);
    }

    [HttpPost]
    [RequirePermission(Permissions.RetainersManage)]
    public async Task<IActionResult> Create(CreateRetainerCommand command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        return Created($"/api/v1/m/services/retainers/{result.Id}", result);
    }

    [HttpPost("{id:guid}/invoice")]
    [RequirePermission(Permissions.RetainersManage)]
    public Task<RetainerInvoiceDto> Invoice(Guid id, RetainerInvoiceRequest? body, CancellationToken ct)
        => Mediator.Send(new GenerateRetainerInvoiceCommand(id, body?.PeriodDate, body?.InvoiceDate, body?.DueDate, body?.TaxCodeId, body?.Notes), ct);

    [HttpPost("{id:guid}/status")]
    [RequirePermission(Permissions.RetainersManage)]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateRetainerStatusCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest();
        var result = await Mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>Optional: a date inside the period to invoice (none means today's), the invoice dates, tax code and notes.</summary>
    public sealed record RetainerInvoiceRequest(DateOnly? PeriodDate = null, DateOnly? InvoiceDate = null, DateOnly? DueDate = null, Guid? TaxCodeId = null, string? Notes = null);
}
