using ERP.Application.Security;
using ERP.Application.Workflow;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Workflow;

/// <summary>Approval policies: which documents need approval, from what amount, by whom.</summary>
[Route("api/v1/approval-policies")]
public sealed class ApprovalPoliciesController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    [RequirePermission(CorePermissions.SettingsRead)]
    public Task<IReadOnlyList<ApprovalPolicyDto>> List(CancellationToken ct)
        => Mediator.Send(new ListApprovalPoliciesQuery(), ct);

    [HttpPost]
    [RequirePermission(CorePermissions.SettingsWrite)]
    public async Task<IActionResult> Create(ApprovalPolicyInput input, CancellationToken ct)
    {
        var created = await Mediator.Send(new SaveApprovalPolicyCommand(null, input), ct);
        return Created($"/api/v1/approval-policies/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(CorePermissions.SettingsWrite)]
    public Task<ApprovalPolicyDto> Update(Guid id, ApprovalPolicyInput input, CancellationToken ct)
        => Mediator.Send(new SaveApprovalPolicyCommand(id, input), ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission(CorePermissions.SettingsWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteApprovalPolicyCommand(id), ct);
        return NoContent();
    }
}
