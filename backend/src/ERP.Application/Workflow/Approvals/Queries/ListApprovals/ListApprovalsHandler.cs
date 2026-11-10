using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Workflow;

internal sealed class ListApprovalsHandler(IApprovalRequestRepository requests, ICurrentUser user) : IQueryHandler<ListApprovalsQuery, IReadOnlyList<ApprovalDto>>
{
    private const int MaxRows = 200;

    public async Task<IReadOnlyList<ApprovalDto>> Handle(ListApprovalsQuery query, CancellationToken ct)
    {
        var me = user.UserId;

        // 1. Unless "mine" was asked for: the pending requests of others whose current level I may decide and have not decided yet.
        if (query.Scope != "mine")
        {
            var pending = await requests.ListPendingRequestedByOthersAsync(me, ct);
            var mayDecide = pending.Where(r => ApprovalAccess.InAllowedEntity(user, r) && user.HasPermission(ApprovalRouting.EffectivePermission(r.CurrentStep?.ApproverPermission)) && !r.Steps.Any(s => s.DecidedBy == me)).Take(MaxRows).ToList();
            if (mayDecide.Count > 0 || user.HasPermission(CorePermissions.ApprovalsDecide)) return mayDecide.Select(ApprovalDto.From).ToList();
        }

        // 2. Otherwise my own requests.
        var mine = await requests.ListRequestedByAsync(me, MaxRows, ct);
        // Requests of no particular company stay visible to everyone the scope above allows.
        return mine.Where(r => ApprovalAccess.InAllowedEntity(user, r)).Select(ApprovalDto.From).ToList();
    }
}
