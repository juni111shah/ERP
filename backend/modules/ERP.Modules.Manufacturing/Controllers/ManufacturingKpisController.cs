using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Manufacturing.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Manufacturing.Controllers;

/// <summary>Manufacturing KPIs.</summary>
[ModuleGate(ManufacturingModule.Key)]
[Route("api/v1/m/manufacturing/kpis")]
public sealed class ManufacturingKpisController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.AnalyticsRead)]
    public Task<ManufacturingKpiSummaryDto> Get(CancellationToken ct)
        => Mediator.Send(new GetManufacturingKpiSummaryQuery(), ct);
}
