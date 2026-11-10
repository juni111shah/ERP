using ERP.Application.Portal;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Portal;

/// <summary>Customer (tenant) self-service: their profile and read-only access to their own documents.</summary>
[Route("api/v1/portal")]
public sealed class PortalController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("me")]
    [Authorize(Policy = "portal")]
    public Task<PortalProfileDto> GetProfile(CancellationToken ct)
        => Mediator.Send(new GetPortalProfileQuery(), ct);

    [HttpGet("invoices")]
    [Authorize(Policy = "portal.customer")]
    public Task<IReadOnlyList<PortalInvoiceDto>> ListInvoices(CancellationToken ct)
        => Mediator.Send(new ListPortalInvoicesQuery(), ct);

    [HttpGet("receipts")]
    [Authorize(Policy = "portal.customer")]
    public Task<IReadOnlyList<PortalReceiptDto>> ListReceipts(CancellationToken ct)
        => Mediator.Send(new ListPortalReceiptsQuery(), ct);

    [HttpGet("statement")]
    [Authorize(Policy = "portal.customer")]
    public Task<PortalStatementDto> GetStatement(DateOnly? asOf, CancellationToken ct)
        => Mediator.Send(new GetPortalStatementQuery(asOf ?? DateOnly.FromDateTime(DateTime.UtcNow)), ct);
}
