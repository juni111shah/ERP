using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;

namespace ERP.Application.Workflow;

internal sealed class SubmitApprovalHandler(IApprovalService approvals, ILegalEntityGuard guard) : ICommandHandler<SubmitApprovalCommand, ApprovalDto>
{
    public async Task<ApprovalDto> Handle(SubmitApprovalCommand command, CancellationToken ct)
    {
        var i = command.Input;
        if (i.LegalEntityId is { } entityId) await guard.RequireAsync(entityId, ct);
        return ApprovalDto.From(await approvals.SubmitAsync("general", null, i.Subject, i.Amount, i.Justification, i.LegalEntityId, ct));
    }
}
