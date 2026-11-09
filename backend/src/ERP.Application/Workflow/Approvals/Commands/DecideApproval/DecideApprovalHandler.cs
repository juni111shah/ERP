using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Workflow;

internal sealed class DecideApprovalHandler(IApprovalService approvals) : ICommandHandler<DecideApprovalCommand, ApprovalDto>
{
    public async Task<ApprovalDto> Handle(DecideApprovalCommand command, CancellationToken ct)
        => ApprovalDto.From(await approvals.DecideAsync(command.Id, command.Approve, command.Comment, ct));
}
