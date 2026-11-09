using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>The payables reports: ageing, vendor statement and vendor ledger.</summary>
[Route("api/v1/finance/reports")]
public sealed class PayablesReportsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("ap-ageing")]
    [RequirePermission(CorePermissions.ReportsRead)]
    public Task<ArAgeingDto> ApAgeing(DateOnly asOf, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetApAgeingQuery(asOf, legalEntityId), ct);

    [HttpGet("vendor-statement")]
    [RequirePermission(CorePermissions.ReportsRead)]
    public Task<VendorStatementDto> VendorStatement(Guid partyId, DateOnly asOf, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetVendorStatementQuery(partyId, asOf, legalEntityId), ct);

    [HttpGet("vendor-ledger")]
    [RequirePermission(CorePermissions.ReportsRead)]
    public Task<PartyLedgerDto> VendorLedger(Guid partyId, DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetVendorLedgerQuery(partyId, from, to, legalEntityId), ct);
}
