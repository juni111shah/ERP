using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Assets.Domain;
using ERP.Modules.Assets.Repositories;

namespace ERP.Modules.Assets.Features;

internal sealed class CreateAssetHandler(IFixedAssetRepository assets, FixedAssetChecks checks, ILegalEntityGuard guard, INumberingService numbering) : ICommandHandler<CreateAssetCommand, AssetDto>
{
    public async Task<AssetDto> Handle(CreateAssetCommand cmd, CancellationToken ct)
    {
        // 1. Entity access, an active category and a business unit of the entity.
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var category = await checks.RequireCategoryAsync(i.CategoryId, ct);
        if (!category.IsActive) throw new FieldValidationException("categoryId", "This category is inactive.");
        await checks.RequireBusinessUnitAsync(i.BusinessUnitId, entity.Id, ct);

        // 2. The draft asset takes the category's method and the next number.
        var asset = new FixedAsset
        {
            LegalEntityId = entity.Id, Number = await numbering.NextAsync(AssetsModule.AssetSequence, entity.Id, i.AcquisitionDate, ct), Method = category.Method, DecliningRatePercent = category.DecliningRatePercent,
        };
        FixedAssetChecks.Apply(asset, i, category);

        // 3. Save.
        assets.Add(asset);
        await assets.SaveChangesAsync(ct);
        return AssetDto.From(asset);
    }
}
