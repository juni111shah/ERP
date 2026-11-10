using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Platform;
using MediatR;

namespace ERP.Application.Workflow;

internal sealed class DeleteApprovalPolicyHandler(IRepository<ApprovalPolicy> policies) : ICommandHandler<DeleteApprovalPolicyCommand>
{
    public async Task<Unit> Handle(DeleteApprovalPolicyCommand command, CancellationToken ct)
    {
        policies.Remove(await policies.GetForUpdateAsync(command.Id, ct));
        await policies.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
