using ERP.Application.Workflow;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Workflow;

/// <summary>Approval requests: submit, decide level by level, withdraw.</summary>
[Route("api/v1/approvals")]
public sealed class ApprovalsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [Authorize]
    public Task<IReadOnlyList<ApprovalDto>> List(string? scope, CancellationToken ct)
        => Mediator.Send(new ListApprovalsQuery(scope), ct);

    [HttpGet("{id:guid}")]
    [Authorize]
    public Task<ApprovalDto> GetById(Guid id, CancellationToken ct)
        => Mediator.Send(new GetApprovalByIdQuery(id), ct);

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Submit(ApprovalInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new SubmitApprovalCommand(input), ct);
        return Created($"/api/v1/approvals/{created.Id}", created);
    }

    /// <summary>The permission differs per level: the approval service checks it.</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize]
    public Task<ApprovalDto> Approve(Guid id, DecisionRequest? decision, CancellationToken ct)
        => Mediator.Send(new DecideApprovalCommand(id, true, decision?.Comment), ct);

    [HttpPost("{id:guid}/reject")]
    [Authorize]
    public Task<ApprovalDto> Reject(Guid id, DecisionRequest? decision, CancellationToken ct)
        => Mediator.Send(new DecideApprovalCommand(id, false, decision?.Comment), ct);

    [HttpPost("{id:guid}/withdraw")]
    [Authorize]
    public Task<ApprovalDto> Withdraw(Guid id, CancellationToken ct)
        => Mediator.Send(new WithdrawApprovalCommand(id), ct);

    /// <summary>Optional comment on an approve or reject decision.</summary>
    public sealed record DecisionRequest(string? Comment);
}
