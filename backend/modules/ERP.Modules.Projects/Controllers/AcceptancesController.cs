using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>Customer sign-offs, staff side: asking for one and following them up.</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects/acceptances")]
public sealed class AcceptancesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.MilestonesRead)]
    public Task<IReadOnlyList<AcceptanceDto>> List(Guid? projectId, CancellationToken ct)
        => Mediator.Send(new ListAcceptancesQuery(projectId), ct);

    [HttpPost]
    [RequirePermission(P.MilestonesManage)]
    public async Task<IActionResult> RequestSignOff(RequestAcceptanceInput input, CancellationToken ct)
        => Created("/api/v1/m/projects/acceptances", await Mediator.Send(new RequestAcceptanceCommand(input), ct));
}
