using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using ERP.Domain.Platform;
using ERP.Modules.RealEstate.Domain;
using MediatR;

namespace ERP.Modules.RealEstate.Features;

internal sealed class VoidOwnerPayoutHandler(
    IRepository<OwnerPayout> payouts, IReadRepository<Bill> bills, IRepository<AuditEvent> audit, OwnerPayoutRelease release, ISender sender, ICurrentUser user, IClock clock, IUnitOfWork uow)
    : ICommandHandler<VoidOwnerPayoutCommand, OwnerPayoutDto>
{
    public Task<OwnerPayoutDto> Handle(VoidOwnerPayoutCommand cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        // 1. A live payout.
        var payout = await payouts.GetForUpdateAsync(cmd.Id, token);
        if (payout.Status == OwnerPayoutStatus.Voided) throw new ConflictException("This payout is already voided.");

        // 2. Release it: through its bill on the bill route, directly otherwise.
        if (payout.BillId is { } billId)
        {
            // voiding or deleting the bill raises BillReleased, which reverses the rest of the payout below
            var bill = await bills.FindAsync(billId, token);
            if (bill?.Status is DocumentStatus.Draft or DocumentStatus.PendingApproval) await sender.Send(new DeleteBillCommand(billId), token);
            else await sender.Send(new VoidBillCommand(billId, cmd.Reason), token);
        }
        else await release.ReleaseAsync(payout, token);

        // 3. Stamp, audit and save.
        VoidMemo.Stamp(payout, cmd.Reason, clock, user);
        VoidMemo.Audit(audit, user, clock, nameof(OwnerPayout), payout.Id, payout.Number, cmd.Reason!);
        await payouts.SaveChangesAsync(token);
        return OwnerPayoutDto.From(payout);
    }, ct);
}
