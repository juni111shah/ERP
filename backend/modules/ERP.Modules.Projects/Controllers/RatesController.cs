using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Projects.Controllers;

/// <summary>Rate cards: bill and cost rates per project, person or role.</summary>
[ModuleGate(ProjectsModule.Key)]
[Route("api/v1/m/projects/rates")]
public sealed class RatesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.RatesRead)]
    public Task<IReadOnlyList<RateDto>> List(Guid? projectId, CancellationToken ct)
        => Mediator.Send(new ListRatesQuery(projectId), ct);

    [HttpPost]
    [RequirePermission(P.RatesManage)]
    public async Task<IActionResult> Create(RateInput input, CancellationToken ct)
        => Created("/api/v1/m/projects/rates", await Mediator.Send(new SaveRateCommand(null, input), ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(P.RatesManage)]
    public Task<RateDto> Update(Guid id, RateInput input, CancellationToken ct)
        => Mediator.Send(new SaveRateCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.RatesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteRateCommand(id), ct);
        return NoContent();
    }
}
