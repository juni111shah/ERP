using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Shifts, who is on them, and the dated roster.</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/shifts")]
public sealed class ShiftsController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string Base = "/api/v1/m/hr";

    [HttpGet]
    [RequirePermission(P.AttendanceRead)]
    public Task<IReadOnlyList<ShiftDto>> List(CancellationToken ct) => Mediator.Send(new ListShiftsQuery(), ct);

    [HttpPost]
    [RequirePermission(P.AttendanceManage)]
    public async Task<IActionResult> Create(ShiftInput input, CancellationToken ct)
        => Created($"{Base}/shifts", await Mediator.Send(new CreateShiftCommand(input), ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(P.AttendanceManage)]
    public Task<ShiftDto> Update(Guid id, ShiftInput input, CancellationToken ct) => Mediator.Send(new UpdateShiftCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.AttendanceManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteShiftCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/assign")]
    [RequirePermission(P.AttendanceManage)]
    public async Task<object> Assign(Guid id, AssignShiftInput b, CancellationToken ct) => new { assigned = await Mediator.Send(new AssignShiftCommand(id, b.EmployeeIds), ct) };

    [HttpPost("unassign")]
    [RequirePermission(P.AttendanceManage)]
    public async Task<object> Unassign(AssignShiftInput b, CancellationToken ct) => new { cleared = await Mediator.Send(new UnassignShiftCommand(b.EmployeeIds), ct) };

    [HttpGet("roster")]
    [RequirePermission(P.AttendanceRead)]
    public Task<IReadOnlyList<RosterDto>> ListRoster(int year, int month, CancellationToken ct) => Mediator.Send(new ListRosterQuery(year, month), ct);

    [HttpPost("{id:guid}/roster")]
    [RequirePermission(P.AttendanceManage)]
    public async Task<IActionResult> AddRoster(Guid id, RosterInput b, CancellationToken ct)
        => Created($"{Base}/shifts/roster", await Mediator.Send(new AddRosterCommand(id, b), ct));

    [HttpDelete("roster/{id:guid}")]
    [RequirePermission(P.AttendanceManage)]
    public async Task<IActionResult> DeleteRoster(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteRosterCommand(id), ct);
        return NoContent();
    }
}
