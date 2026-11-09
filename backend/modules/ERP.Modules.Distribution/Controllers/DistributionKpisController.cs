using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Distribution.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Distribution.Controllers;

/// <summary>Fleet analytics and KPIs.</summary>
[ModuleGate(DistributionModule.Key)]
[Route("api/v1/m/distribution/kpis")]
public sealed class DistributionKpisController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.AnalyticsRead)]
    public Task<DistributionKpiSummaryDto> Get(CancellationToken ct)
        => Mediator.Send(new GetDistributionKpiSummaryQuery(), ct);
}
