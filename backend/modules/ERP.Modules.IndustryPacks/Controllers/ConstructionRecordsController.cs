using ERP.Modularity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.IndustryPacks.Controllers;

/// <summary>The records of the Construction pack (the shared pack routes, see <see cref="PackRecordsControllerBase"/>).</summary>
[ModuleGate("construction")]
[Route("api/v1/m/construction")]
[Authorize]
public sealed class ConstructionRecordsController(ISender mediator) : PackRecordsControllerBase(mediator)
{
    protected override string PackKey => "construction";
}