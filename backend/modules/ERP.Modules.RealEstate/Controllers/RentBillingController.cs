using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>Rent billing: the billing run, invoicing one instalment, and the lease links of rent invoices.</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/billing")]
public sealed class RentBillingController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("invoice-links")]
    [RequirePermission(P.LeasesRead)]
    public Task<IReadOnlyList<InvoiceLeaseLinkDto>> GetInvoiceLinks([FromQuery] Guid[] ids, CancellationToken ct)
        => Mediator.Send(new GetInvoiceLeaseLinksQuery(ids), ct);

    [HttpPost("run")]
    [RequirePermission(P.BillingRun)]
    public Task<BillingResult> Run(BillingRunRequest body, CancellationToken ct)
        => Mediator.Send(new RunBillingCommand(body.AsOf, body.LegalEntityId), ct);

    [HttpPost("instalments/{id:guid}/invoice")]
    [RequirePermission(P.BillingRun)]
    public Task<InstalmentInvoiceDto> InvoiceInstalment(Guid id, CancellationToken ct)
        => Mediator.Send(new InvoiceInstalmentCommand(id), ct);

    public sealed record BillingRunRequest(DateOnly AsOf, Guid? LegalEntityId = null);
}
