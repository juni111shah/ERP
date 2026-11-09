using ERP.Application.Abstractions;
using ERP.Application.Finance;
using ERP.Modules.RealEstate.Repositories;
using MediatR;

namespace ERP.Modules.RealEstate.Features;

/// <summary>The owner's bill was voided or deleted (here or on the bills screen): the payout goes with it, so the period can be paid again.</summary>
internal sealed class OwnerPayoutBillReleasedHandler(IOwnerPayoutRepository payouts, OwnerPayoutRelease release, ICurrentUser user, IClock clock) : INotificationHandler<BillReleased>
{
    public async Task Handle(BillReleased n, CancellationToken ct)
    {
        var payout = await payouts.FindPostedForBillForUpdateAsync(n.BillId, ct);
        if (payout is null) return;
        await release.ReleaseAsync(payout, ct);
        VoidMemo.Stamp(payout, "The owner's bill was voided", clock, user);
        await payouts.SaveChangesAsync(ct);
    }
}
