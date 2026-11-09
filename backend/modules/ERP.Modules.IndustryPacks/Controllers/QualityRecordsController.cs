using ERP.Modularity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.IndustryPacks.Controllers;

/// <summary>The records of the Quality pack (the shared pack routes, see <see cref="PackRecordsControllerBase"/>).</summary>
[ModuleGate("quality")]
[Route("api/v1/m/quality")]
[Authorize]
public sealed class QualityRecordsController(ISender mediator) : PackRecordsControllerBase(mediator)
{
    protected override string PackKey => "quality";
}