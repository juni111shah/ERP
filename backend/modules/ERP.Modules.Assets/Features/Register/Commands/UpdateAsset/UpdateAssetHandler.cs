using ERP.Application.Common;
using ERP.Modules.Assets.Domain;
using ERP.Modules.Assets.Repositories;

namespace ERP.Modules.Assets.Features;

internal sealed class UpdateAssetHandler(IFixedAssetRepository assets, FixedAssetChecks checks) : ICommandHandler<UpdateAssetCommand, AssetDto>
{
    public async Task<AssetDto> Handle(UpdateAssetCommand cmd, CancellationToken ct)
    {
        // 1. Load the asset; its legal entity never changes and the business unit must be of it.
        var asset = await assets.GetForUpdateAsync(cmd.Id, ct);
        var i = cmd.Input;
        if (i.LegalEntityId != asset.LegalEntityId) throw new FieldValidationException("legalEntityId", "The legal entity of an asset cannot change.");
        await checks.RequireBusinessUnitAsync(i.BusinessUnitId, asset.LegalEntityId, ct);

        // 2. A draft takes every change (a new category brings its method); a capitalised asset only the descriptive ones.
        if (asset.Status == AssetStatus.Draft)
        {
            var category = await checks.RequireCategoryAsync(i.CategoryId, ct);
            if (category.Id != asset.CategoryId) { asset.Method = category.Method; asset.DecliningRatePercent = category.DecliningRatePercent; }
            FixedAssetChecks.Apply(asset, i, category);
        }
        else
        {
            // The ledger holds these amounts now; only what does not change a figure can still be edited.
            if (i.CategoryId != asset.CategoryId || i.AcquisitionDate != asset.AcquisitionDate || i.Cost != asset.Cost || i.SalvageValue != asset.SalvageValue || (i.LifeMonths is { } life && life != asset.LifeMonths))
                throw new ConflictException("The cost, salvage value, life, category and acquisition date cannot change once an asset is capitalised.");
            asset.Name = i.Name.Trim(); asset.Description = i.Description?.Trim(); asset.BusinessUnitId = i.BusinessUnitId; asset.Custodian = i.Custodian?.Trim();
        }

        // 3. Save.
        await assets.SaveChangesAsync(ct);
        return AssetDto.From(asset);
    }
}
