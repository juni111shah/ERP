using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Services.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Services.Controllers;

/// <summary>Services analytics.</summary>
[ModuleGate(ServicesModule.Key)]
[Route("api/v1/m/services/analytics")]
public sealed class ServicesAnalyticsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("kpis")]
    [RequirePermission(Permissions.AnalyticsRead)]
    public async Task<IActionResult> Kpis(Guid legalEntityId, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetServicesKpisQuery(legalEntityId), ct);
        return Ok(result);
    }
}
