using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.RealEstate.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.RealEstate.Controllers;

/// <summary>Preventive maintenance schedules and the run that raises their work orders.</summary>
[ModuleGate(RealEstateModule.Key)]
[Route("api/v1/m/realestate/pm-schedules")]
public sealed class PmSchedulesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.PmRead)]
    public Task<PagedResult<PmScheduleDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, PmScheduleDto.Projection, P.PmRead, ct);

    [HttpPost]
    [RequirePermission(P.PmManage)]
    public async Task<IActionResult> Create(PmScheduleInput input, CancellationToken ct)
        => Created("/api/v1/m/realestate/pm-schedules", await Mediator.Send(new CreatePmScheduleCommand(input), ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(P.PmManage)]
    public Task<PmScheduleDto> Update(Guid id, PmScheduleInput input, CancellationToken ct)
        => Mediator.Send(new UpdatePmScheduleCommand(id, input), ct);

    [HttpPost("run")]
    [RequirePermission(P.PmManage)]
    public Task<PmRunResultDto> Run(CancellationToken ct)
        => Mediator.Send(new RunPreventiveMaintenanceCommand(), ct);
}
