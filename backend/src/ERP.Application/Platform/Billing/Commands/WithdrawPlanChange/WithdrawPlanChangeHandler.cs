using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class WithdrawPlanChangeHandler(IWorkspaceSubscriptionRepository subscriptions, BillingOverviews overviews, ICurrentUser user) : ICommandHandler<WithdrawPlanChangeCommand, BillingOverview>
{
    public async Task<BillingOverview> Handle(WithdrawPlanChangeCommand command, CancellationToken ct)
    {
        var orgId = user.OrgId ?? throw new ForbiddenException();
        var s = await subscriptions.FindForClientForUpdateAsync(orgId, ct) ?? throw new ConflictException("This workspace has no subscription.");
        s.RequestedPlanId = null; s.RequestedCycle = null; s.RequestNote = null; s.RequestedAt = null;
        await subscriptions.SaveChangesAsync(ct);
        return await overviews.GetAsync(orgId, ct);
    }
}
