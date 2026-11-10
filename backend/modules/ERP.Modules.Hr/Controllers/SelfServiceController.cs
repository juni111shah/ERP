using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Self-service: signed-in users with a linked employee need no permission; the employee comes from the link.</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/me")]
[Authorize]
public sealed class SelfServiceController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string Base = "/api/v1/m/hr";

    [HttpGet]
    public Task<EmployeeDto> Get(CancellationToken ct) => Mediator.Send(new GetMyEmployeeQuery(), ct);

    [HttpGet("leave")]
    public Task<PagedResult<LeaveRequestRow>> ListLeave([FromQuery] PagedQuery query, CancellationToken ct) => Mediator.Send(new ListMyLeaveQuery(query), ct);

    [HttpPost("leave")]
    public async Task<IActionResult> CreateLeave(MyLeaveInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateMyLeaveCommand(input), ct);
        return Created($"{Base}/me/leave/{created.Id}", created);
    }

    [HttpPost("leave/{id:guid}/submit")]
    public Task<LeaveRequestDto> SubmitLeave(Guid id, CancellationToken ct) => Mediator.Send(new SubmitMyLeaveCommand(id), ct);

    [HttpPost("leave/{id:guid}/cancel")]
    public Task<LeaveRequestDto> CancelLeave(Guid id, CancellationToken ct) => Mediator.Send(new CancelMyLeaveCommand(id), ct);

    [HttpGet("balances")]
    public Task<IReadOnlyList<LeaveBalanceRow>> Balances(int? year, CancellationToken ct) => Mediator.Send(new GetMyBalancesQuery(year), ct);

    [HttpGet("payslips")]
    public Task<IReadOnlyList<MyPayslipDto>> ListPayslips(CancellationToken ct) => Mediator.Send(new ListMyPayslipsQuery(), ct);

    [HttpGet("payslips/{lineId:guid}")]
    public Task<MyPayslipDto> GetPayslip(Guid lineId, CancellationToken ct) => Mediator.Send(new GetMyPayslipQuery(lineId), ct);

    [HttpGet("team/leave")]
    public Task<PagedResult<LeaveRequestRow>> ListTeamLeave([FromQuery] PagedQuery query, string? status, CancellationToken ct) => Mediator.Send(new ListTeamLeaveQuery(query, status), ct);
}
