using ERP.Api.Infrastructure;
using ERP.Application.Common;
using ERP.Application.Platform;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.PlatformAdmin;

/// <summary>Registrations waiting for a person (platform console).</summary>
[Route("api/v1/platform-admin/signups")]
[Authorize(Policy = PlatformAdminRequirement.Policy)]
public sealed class SignupsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    public Task<PagedResult<SignupRow>> List([FromQuery] ListSignupsQuery query, CancellationToken ct) => Mediator.Send(query, ct);

    [PlatformManager]
    [HttpPost("{id:guid}/approve")]
    public Task<SignupRow> Approve(Guid id, CancellationToken ct) => Mediator.Send(new ApproveSignupCommand(id), ct);

    [PlatformManager]
    [HttpPost("{id:guid}/reject")]
    public Task<SignupRow> Reject(Guid id, ReasonInput input, CancellationToken ct) => Mediator.Send(new RejectSignupCommand(id, input.Reason), ct);
}
