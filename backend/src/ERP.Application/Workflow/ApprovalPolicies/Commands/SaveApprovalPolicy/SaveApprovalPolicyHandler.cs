using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;

namespace ERP.Application.Workflow;

internal sealed class SaveApprovalPolicyHandler(IRepository<ApprovalPolicy> policies) : ICommandHandler<SaveApprovalPolicyCommand, ApprovalPolicyDto>
{
    public async Task<ApprovalPolicyDto> Handle(SaveApprovalPolicyCommand command, CancellationToken ct)
    {
        // 1. Load the policy to update, or start a new one.
        var policy = command.Id is { } id ? await policies.GetForUpdateAsync(id, ct) : new ApprovalPolicy();

        // 2. Apply the input; a blank approver permission or level keeps the current (default) one.
        policy.DocumentType = command.Input.DocumentType; policy.MinAmount = command.Input.MinAmount; policy.IsActive = command.Input.IsActive;
        if (!string.IsNullOrWhiteSpace(command.Input.ApproverPermission)) policy.ApproverPermission = command.Input.ApproverPermission;
        if (command.Input.Level is { } level) policy.Level = level;

        // 3. Save.
        if (command.Id is null) policies.Add(policy);
        await policies.SaveChangesAsync(ct);
        return ApprovalPolicyDto.From(policy);
    }
}
