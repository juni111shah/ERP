using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Assets.Domain;
using MediatR;

namespace ERP.Modules.Assets.Features;

/// <summary>A capitalised asset depends on its bill: voiding the bill would take the cost off the books under the asset.</summary>
public sealed class AssetBillGuard(IReadRepository<FixedAsset> assets) : INotificationHandler<BillReleased>
{
    public async Task Handle(BillReleased n, CancellationToken ct)
    {
        if (await assets.AnyAsync(a => a.SourceBillId == n.BillId && a.Status != AssetStatus.Draft, ct))
            throw new ConflictException("A fixed asset was capitalised from this bill, so it cannot be voided. Dispose of the asset first.");
    }
}
