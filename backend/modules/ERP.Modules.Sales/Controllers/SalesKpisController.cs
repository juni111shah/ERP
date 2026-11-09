using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Sales.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Sales.Controllers;

/// <summary>Sales KPIs.</summary>
[ModuleGate(SalesModule.Key)]
[Route("api/v1/m/sales/kpis")]
public sealed class SalesKpisController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.OrdersRead)]
    public Task<SalesKpiDto> Get(CancellationToken ct)
        => Mediator.Send(new GetSalesKpiSummaryQuery(), ct);
}
