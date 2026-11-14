using ERP.Modularity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.IndustryPacks.Controllers;

/// <summary>The records of the Agriculture pack (the shared pack routes, see <see cref="PackRecordsControllerBase"/>).</summary>
[ModuleGate("agriculture")]
[Route("api/v1/m/agriculture")]
public sealed class AgricultureRecordsController(ISender mediator) : PackRecordsControllerBase(mediator)
{
    protected override string PackKey => "agriculture";
}