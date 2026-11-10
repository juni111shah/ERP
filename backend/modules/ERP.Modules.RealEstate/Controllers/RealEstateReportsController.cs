using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>Real estate reports: owner statement, rent roll, occupancy, performance, expiries, maintenance and commissions.</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/reports")]
[RequirePermission(CorePermissions.ReportsRead)]
public sealed class RealEstateReportsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("owner-statement")]
    public Task<OwnerStatementDto> OwnerStatement(Guid ownerPartyId, DateOnly from, DateOnly to, CancellationToken ct)
        => Mediator.Send(new GetOwnerStatementQuery(ownerPartyId, from, to), ct);

    [HttpGet("rent-roll")]
    public Task<RentRollDto> RentRoll(DateOnly asOf, [FromQuery] Guid[]? propertyIds, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetRentRollQuery(asOf, propertyIds, legalEntityId), ct);

    [HttpGet("occupancy")]
    public Task<OccupancyDto> Occupancy(Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetOccupancyReportQuery(legalEntityId), ct);

    [HttpGet("portfolio-performance")]
    public Task<PerformanceDto> PortfolioPerformance(DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetPortfolioPerformanceQuery(from, to, legalEntityId), ct);

    [HttpGet("lease-expiries")]
    public Task<LeaseExpiriesDto> LeaseExpiries(int? months, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetLeaseExpiriesQuery(months ?? 12, legalEntityId), ct);

    [HttpGet("maintenance-costs")]
    public Task<MaintenanceCostsDto> MaintenanceCosts(DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetMaintenanceCostsQuery(from, to, legalEntityId), ct);

    [HttpGet("commissions")]
    public Task<CommissionsDto> Commissions(DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetCommissionsReportQuery(from, to, legalEntityId), ct);
}
