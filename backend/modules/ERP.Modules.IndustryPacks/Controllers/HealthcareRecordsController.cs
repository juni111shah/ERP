using ERP.Modularity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.IndustryPacks.Controllers;

/// <summary>The records of the Healthcare pack (the shared pack routes, see <see cref="PackRecordsControllerBase"/>).</summary>
[ModuleGate("healthcare")]
[Route("api/v1/m/healthcare")]
[Authorize]
public sealed class HealthcareRecordsController(ISender mediator) : PackRecordsControllerBase(mediator)
{
    protected override string PackKey => "healthcare";
}