using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>Risks and issues of projects.</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects/risks")]
public sealed class RisksController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.RisksRead)]
    public Task<IReadOnlyList<RiskDto>> List(Guid? projectId, CancellationToken ct)
        => Mediator.Send(new ListRisksQuery(projectId), ct);

    [HttpPost]
    [RequirePermission(P.RisksManage)]
    public async Task<IActionResult> Create(RiskInput input, CancellationToken ct)
        => Created("/api/v1/m/projects/risks", await Mediator.Send(new SaveRiskCommand(null, input), ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(P.RisksManage)]
    public Task<RiskDto> Update(Guid id, RiskInput input, CancellationToken ct)
        => Mediator.Send(new SaveRiskCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.RisksManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteRiskCommand(id), ct);
        return NoContent();
    }
}
