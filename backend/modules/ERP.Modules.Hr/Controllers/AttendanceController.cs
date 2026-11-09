using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Attendance: the monthly exceptions payroll reads, and the daily logs (manual, imported, punched) rolled up into them.</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/attendance")]
public sealed class AttendanceController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(P.AttendanceRead)]
    public Task<IReadOnlyList<AttendanceRowDto>> List(int year, int month, Guid? legalEntityId, CancellationToken ct) => Mediator.Send(new ListAttendanceQuery(year, month, legalEntityId), ct);

    [HttpPut]
    [RequirePermission(P.AttendanceManage)]
    public Task<AttendanceRowDto> Save(AttendanceInput input, CancellationToken ct) => Mediator.Send(new SaveAttendanceCommand(input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(P.AttendanceManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteAttendanceCommand(id), ct);
        return NoContent();
    }

    [HttpGet("daily")]
    [RequirePermission(P.AttendanceRead)]
    public Task<IReadOnlyList<DailyAttendanceDto>> ListDaily(int year, int month, Guid? employeeId, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new ListDailyAttendanceQuery(year, month, employeeId, legalEntityId), ct);

    [HttpPost("punch")]
    [RequirePermission(P.AttendanceManage)]
    public Task<BulkDailyAttendanceResult> Punch(PunchesRequest body, CancellationToken ct) => Mediator.Send(new RecordPunchesCommand(body.Punches, body.Source), ct);

    [HttpPut("daily")]
    [RequirePermission(P.AttendanceManage)]
    public Task<DailyAttendanceDto> SaveDaily(DailyAttendanceInput input, CancellationToken ct) => Mediator.Send(new SaveDailyAttendanceCommand(input), ct);

    [HttpPost("daily/bulk")]
    [RequirePermission(P.AttendanceManage)]
    public Task<BulkDailyAttendanceResult> BulkSaveDaily(BulkDailyRequest body, CancellationToken ct) => Mediator.Send(new BulkSaveDailyAttendanceCommand(body.Items), ct);

    [HttpPost("daily/roll-up")]
    [RequirePermission(P.AttendanceManage)]
    public Task<IReadOnlyList<AttendanceRollUpRow>> RollUp(RollUpRequest body, CancellationToken ct) => Mediator.Send(new RollUpAttendanceCommand(body.Year, body.Month, body.LegalEntityId), ct);

    [HttpDelete("daily/{id:guid}")]
    [RequirePermission(P.AttendanceManage)]
    public async Task<IActionResult> DeleteDaily(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteDailyAttendanceCommand(id), ct);
        return NoContent();
    }

    /// <summary>Raw clock punches and where they came from (default "device").</summary>
    public sealed record PunchesRequest(IReadOnlyList<PunchInput> Punches, string? Source = null);

    /// <summary>Days of attendance to import.</summary>
    public sealed record BulkDailyRequest(IReadOnlyList<DailyAttendanceInput> Items);

    /// <summary>The month to roll up, of one legal entity or (null) all.</summary>
    public sealed record RollUpRequest(int Year, int Month, Guid? LegalEntityId);
}
