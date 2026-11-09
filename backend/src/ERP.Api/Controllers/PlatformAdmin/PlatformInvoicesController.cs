using ERP.Api.Infrastructure;
using ERP.Application.Common;
using ERP.Application.Platform;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.PlatformAdmin;

/// <summary>The platform's invoices to its clients (platform console).</summary>
[Route("api/v1/platform-admin/invoices")]
[Authorize(Policy = PlatformAdminRequirement.Policy)]
public sealed class PlatformInvoicesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    public Task<PagedResult<PlatformInvoiceRow>> List([FromQuery] ListPlatformInvoicesQuery query, CancellationToken ct) => Mediator.Send(query, ct);

    [HttpGet("{id:guid}")]
    public Task<InvoiceDocument> Get(Guid id, CancellationToken ct) => Mediator.Send(new GetPlatformInvoiceQuery(id), ct);

    [HttpPost("{id:guid}/payment")]
    public Task<InvoiceDocument> RecordPayment(Guid id, RecordPaymentInput input, CancellationToken ct) => Mediator.Send(new RecordInvoicePaymentCommand(id, input), ct);

    [HttpPost("{id:guid}/void")]
    public Task<InvoiceDocument> Void(Guid id, ReasonInput input, CancellationToken ct) => Mediator.Send(new VoidPlatformInvoiceCommand(id, input.Reason), ct);

    [HttpPost("{id:guid}/reopen")]
    public Task<InvoiceDocument> Reopen(Guid id, CancellationToken ct) => Mediator.Send(new ReopenPlatformInvoiceCommand(id), ct);
}
