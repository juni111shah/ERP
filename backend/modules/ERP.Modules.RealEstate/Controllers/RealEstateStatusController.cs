using ERP.Modularity;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>Tells the client the real estate module is installed and enabled.</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/status")]
public sealed class RealEstateStatusController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.PropertiesRead)]
    public IActionResult Get() => Ok(new { module = RealEstateModule.Key, message = "Real estate module is installed and enabled." });
}
