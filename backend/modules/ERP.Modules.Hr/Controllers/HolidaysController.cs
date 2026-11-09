using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Public holidays (left out of leave day counts).</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/holidays")]
public sealed class HolidaysController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string Base = "/api/v1/m/hr";

    [HttpGet]
    [RequirePermission(P.LeaveRead)]
    public Task<PagedResult<HolidayDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, HolidayDto.Projection, P.LeaveRead, ct);

    [HttpPost]
    [RequirePermission(P.LeaveApprove)]
    public async Task<IActionResult> Create(HolidayInput input, CancellationToken ct)
        => Created($"{Base}/holidays", await Mediator.Send(new CreateHolidayCommand(input), ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(P.LeaveApprove)]
    public Task<HolidayDto> Update(Guid id, HolidayInput input, CancellationToken ct) => Mediator.Send(new UpdateHolidayCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.LeaveApprove)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteHolidayCommand(id), ct);
        return NoContent();
    }
}
