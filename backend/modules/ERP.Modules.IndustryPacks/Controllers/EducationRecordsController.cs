using ERP.Modularity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.IndustryPacks.Controllers;

/// <summary>The records of the Education pack (the shared pack routes, see <see cref="PackRecordsControllerBase"/>).</summary>
[ModuleGate("education")]
[Route("api/v1/m/education")]
[Authorize]
public sealed class EducationRecordsController(ISender mediator) : PackRecordsControllerBase(mediator)
{
    protected override string PackKey => "education";
}