using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Workflow;

internal sealed class WithdrawApprovalHandler(IApprovalService approvals) : ICommandHandler<WithdrawApprovalCommand, ApprovalDto>
{
    public async Task<ApprovalDto> Handle(WithdrawApprovalCommand command, CancellationToken ct)
        => ApprovalDto.From(await approvals.WithdrawAsync(command.Id, ct));
}
