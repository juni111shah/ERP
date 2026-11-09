using ERP.Modularity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.IndustryPacks.Controllers;

/// <summary>The records of the Hospitality pack (the shared pack routes, see <see cref="PackRecordsControllerBase"/>).</summary>
[ModuleGate("hospitality")]
[Route("api/v1/m/hospitality")]
[Authorize]
public sealed class HospitalityRecordsController(ISender mediator) : PackRecordsControllerBase(mediator)
{
    protected override string PackKey => "hospitality";
}