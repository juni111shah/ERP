using ERP.Application.Common;
using ERP.Modules.Assets.Domain;
using ERP.Modules.Assets.Repositories;
using MediatR;

namespace ERP.Modules.Assets.Features;

internal sealed class DeleteAssetHandler(IFixedAssetRepository assets) : ICommandHandler<DeleteAssetCommand>
{
    public async Task<Unit> Handle(DeleteAssetCommand cmd, CancellationToken ct)
    {
        var asset = await assets.GetForUpdateAsync(cmd.Id, ct);
        if (asset.Status != AssetStatus.Draft) throw new ConflictException("Only an asset that was never capitalised can be deleted. Dispose of it instead.");
        assets.Remove(asset);
        await assets.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
