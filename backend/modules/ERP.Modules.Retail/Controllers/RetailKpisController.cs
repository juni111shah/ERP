using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Retail.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Retail.Controllers;

/// <summary>Retail analytics and KPIs.</summary>
[ModuleGate(RetailModule.Key)]
[Route("api/v1/m/retail/kpis")]
public sealed class RetailKpisController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(Permissions.AnalyticsRead)]
    public Task<RetailKpiSummaryDto> Get(CancellationToken ct)
        => Mediator.Send(new GetRetailKpiSummaryQuery(), ct);
}
