using ERP.Application.Portal;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Portal;

/// <summary>Vendor self-service: read-only access to their own bills, payments and statement.</summary>
[Route("api/v1/portal/vendor")]
[Authorize(Policy = "portal.vendor")]
public sealed class PortalVendorController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("bills")]
    public Task<IReadOnlyList<PortalBillDto>> ListBills(CancellationToken ct)
        => Mediator.Send(new ListPortalVendorBillsQuery(), ct);

    [HttpGet("payments")]
    public Task<IReadOnlyList<PortalPaymentDto>> ListPayments(CancellationToken ct)
        => Mediator.Send(new ListPortalVendorPaymentsQuery(), ct);

    [HttpGet("statement")]
    public Task<PortalPayablesDto> GetStatement(DateOnly? asOf, CancellationToken ct)
        => Mediator.Send(new GetPortalVendorStatementQuery(asOf ?? DateOnly.FromDateTime(DateTime.UtcNow)), ct);
}
