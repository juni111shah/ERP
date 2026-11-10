using ERP.Application.Common;
using ERP.Modularity;
using ERP.Modularity.Web;
using ERP.Modules.Hr.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Modules.Hr.Controllers;

/// <summary>Leave types with their entitlement tiers and pay bands.</summary>
[ModuleGate(HrModule.Key)]
[Route("api/v1/m/hr/leave-types")]
public sealed class LeaveTypesController(ISender mediator) : ApiControllerBase(mediator)
{
    private const string Base = "/api/v1/m/hr";

    [HttpGet]
    [RequirePermission(P.LeaveRead)]
    public Task<PagedResult<LeaveTypeDto>> List([FromQuery] PagedQuery query, CancellationToken ct)
        => PagedList(query, LeaveTypeDto.Projection, P.LeaveRead, ct);

    [HttpPost]
    [RequirePermission(P.LeaveApprove)]
    public async Task<IActionResult> Create(LeaveTypeInput input, CancellationToken ct)
        => Created($"{Base}/leave-types", await Mediator.Send(new CreateLeaveTypeCommand(input), ct));

    [HttpPut("{id:guid}")]
    [RequirePermission(P.LeaveApprove)]
    public Task<LeaveTypeDto> Update(Guid id, LeaveTypeInput input, CancellationToken ct) => Mediator.Send(new UpdateLeaveTypeCommand(id, input), ct);
}
