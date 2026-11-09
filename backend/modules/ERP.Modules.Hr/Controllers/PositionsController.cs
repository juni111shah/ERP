using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Positions (job titles and grades).</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/positions")]
public sealed class PositionsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.EmployeesRead)]
    public Task<PagedResult<PositionDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, PositionDto.Projection, P.EmployeesRead, ct);

    [HttpPost]
    [RequirePermission(P.EmployeesManage)]
    public async Task<IActionResult> Create(PositionInput input, CancellationToken ct)
        => Created("/api/v1/m/hr/positions", await Mediator.Send(new CreatePositionCommand(input), ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(P.EmployeesManage)]
    public Task<PositionDto> Update(Guid id, PositionInput input, CancellationToken ct)
        => Mediator.Send(new UpdatePositionCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.EmployeesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeletePositionCommand(id), ct);
        return NoContent();
    }
}
