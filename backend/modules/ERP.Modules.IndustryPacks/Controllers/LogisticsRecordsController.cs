using ERP.Modularity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.IndustryPacks.Controllers;

/// <summary>The records of the Logistics pack (the shared pack routes, see <see cref="PackRecordsControllerBase"/>).</summary>
[ModuleGate("logistics")]
[Route("api/v1/m/logistics")]
public sealed class LogisticsRecordsController(ISender mediator) : PackRecordsControllerBase(mediator)
{
    protected override string PackKey => "logistics";
}