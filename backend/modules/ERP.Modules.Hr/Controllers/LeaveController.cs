using ERP.Application.Common;
using ERP.Domain.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Leave requests, balances and the working-day count; country defaults for leave types and holidays.</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/leave")]
public sealed class LeaveController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string Base = "/api/v1/m/hr";

    [HttpGet]
    [RequirePermission(P.LeaveRead)]
    public Task<PagedResult<LeaveRequestRow>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, LeaveRequestRow.Projection, P.LeaveRead, ct);

    [HttpPost("apply-country-defaults")]
    [RequirePermission(P.LeaveApprove)]
    public Task<LeaveDefaultsResult> ApplyCountryDefaults(int? year, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new ApplyLeaveCountryDefaultsCommand(year ?? AppClock.UtcNow.Year, legalEntityId), ct);

    [HttpGet("days")]
    [RequirePermission(P.LeaveRead)]
    public Task<LeaveDaysDto> Days(DateOnly from, DateOnly to, CancellationToken ct) => Mediator.Send(new GetLeaveDaysQuery(from, to), ct);

    [HttpGet("balances")]
    [RequirePermission(P.LeaveRead)]
    public Task<IReadOnlyList<LeaveBalanceRow>> Balances(int? year, Guid? employeeId, Guid? legalEntityId, CancellationToken ct)
        => Mediator.Send(new GetLeaveBalancesQuery(year ?? AppClock.UtcNow.Year, employeeId, legalEntityId), ct);

    [HttpGet("{id:guid}")]
    [RequirePermission(P.LeaveRead)]
    public Task<LeaveRequestDto> Get(Guid id, CancellationToken ct) => Mediator.Send(new GetLeaveRequestQuery(id), ct);

    [HttpPost]
    [RequirePermission(P.LeaveRequest)]
    public async Task<IActionResult> Create(LeaveRequestInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new CreateLeaveRequestCommand(input), ct);
        return Created($"{Base}/leave/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(P.LeaveRequest)]
    public Task<LeaveRequestDto> Update(Guid id, LeaveRequestInput input, CancellationToken ct) => Mediator.Send(new UpdateLeaveRequestCommand(id, input), ct);

    [HttpPost("{id:guid}/submit")]
    [RequirePermission(P.LeaveRequest)]
    public Task<LeaveRequestDto> Submit(Guid id, CancellationToken ct) => Mediator.Send(new SubmitLeaveRequestCommand(id), ct);

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(P.LeaveRequest)]
    public Task<LeaveRequestDto> Cancel(Guid id, CancellationToken ct) => Mediator.Send(new CancelLeaveRequestCommand(id), ct);

    // approver permission or line manager: checked in the handler
    [HttpPost("{id:guid}/approve")]
    [Authorize]
    public Task<LeaveRequestDto> Approve(Guid id, LeaveDecisionInput? body, CancellationToken ct) => Mediator.Send(new ApproveLeaveRequestCommand(id, body?.Comment), ct);

    [HttpPost("{id:guid}/reject")]
    [Authorize]
    public Task<LeaveRequestDto> Reject(Guid id, LeaveDecisionInput? body, CancellationToken ct) => Mediator.Send(new RejectLeaveRequestCommand(id, body?.Comment), ct);
}
