using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Finance;

/// <summary>The receivables reports: ageing, customer statement and customer ledger.</summary>
[Route("api/v1/finance/reports")]
public sealed class ReceivablesReportsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("ar-ageing")]
    [RequirePermission(CorePermissions.ReportsRead)]
    public Task<ArAgeingDto> ArAgeing(DateOnly asOf, Guid? legalEntityId, Guid? businessUnitId, string? sourceType, string? groupBy, Guid? partyGroupId, CancellationToken ct)
        => Mediator.Send(new GetArAgeingQuery(asOf, legalEntityId, businessUnitId, sourceType, groupBy, partyGroupId), ct);

    [HttpGet("customer-statement")]
    [RequirePermission(CorePermissions.ReportsRead)]
    public Task<CustomerStatementDto> CustomerStatement(Guid partyId, DateOnly asOf, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetCustomerStatementQuery(partyId, asOf, legalEntityId), ct);

    [HttpGet("customer-ledger")]
    [RequirePermission(CorePermissions.ReportsRead)]
    public Task<PartyLedgerDto> CustomerLedger(Guid partyId, DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetCustomerLedgerQuery(partyId, from, to, legalEntityId), ct);
}
