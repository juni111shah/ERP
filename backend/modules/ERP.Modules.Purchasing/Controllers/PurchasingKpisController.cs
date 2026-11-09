using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Purchasing.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Purchasing.Controllers;

/// <summary>Purchasing KPIs.</summary>
[ModuleGate(PurchasingModule.Key)]
[Route("api/v1/m/purchasing/kpis")]
public sealed class PurchasingKpisController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.OrdersRead)]
    public Task<PurchasingKpiDto> Get(CancellationToken ct)
        => Mediator.Send(new GetPurchasingKpiSummaryQuery(), ct);
}
