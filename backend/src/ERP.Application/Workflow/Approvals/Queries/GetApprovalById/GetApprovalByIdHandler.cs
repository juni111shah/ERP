using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Workflow;

internal sealed class GetApprovalByIdHandler(IApprovalRequestRepository requests, ICurrentUser user) : IQueryHandler<GetApprovalByIdQuery, ApprovalDto>
{
    public async Task<ApprovalDto> Handle(GetApprovalByIdQuery query, CancellationToken ct)
    {
        // 1. Load it with its steps.
        var r = await requests.GetWithStepsAsync(query.Id, ct);

        // 2. Only the requester, auditors and approvers (of any of its levels) in an allowed company see it; for anyone else it does not exist.
        var canSee = r.RequestedBy == user.UserId || user.HasPermission(CorePermissions.AuditRead) || user.HasPermission(CorePermissions.ApprovalsDecide)
            || r.Steps.Any(s => user.HasPermission(ApprovalRouting.EffectivePermission(s.ApproverPermission)));
        return canSee && ApprovalAccess.InAllowedEntity(user, r) ? ApprovalDto.From(r) : throw new NotFoundException();
    }
}
